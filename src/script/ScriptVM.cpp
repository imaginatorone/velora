
#include "ScriptVM.h"
#include "core/Engine.h"
#include "core/GameStateManager.h"
#include "render/Renderer.h"
#include "render/TweenManager.h"
#include "render/ParticleSystem.h"
#include "audio/AudioMixer.h"
#include "text/RichTextParser.h"
#include "i18n/LocalizationManager.h"
#include "asset/AssetManager.h"
#include "core/Paths.h"
#include "core/SystemInfo.h"

#include <iostream>
#include <algorithm>
#include <filesystem>
#include <ctime>
#include <fstream>
#include <stdexcept>
#include <SDL3/SDL.h>

namespace velora::script {

ScriptVM::ScriptVM(velora::core::Engine* engine) : m_engine(engine) {}
ScriptVM::~ScriptVM() = default;

void ScriptVM::loadProgram(Program program) {
    m_program = std::move(program);
    m_pc = 0;
    m_vmState = VMState::RUNNING;
    m_variables.clear();
    m_characters.clear();
    m_currentLabel.clear();

    // Definitions before `start` form the script's initial state.
    if (m_program.labelIndex.count("start")) {
        size_t startIdx = m_program.labelIndex["start"];
        for (size_t i = 0; i < startIdx && i < m_program.statements.size(); ++i) {
            const auto& data = m_program.statements[i].data;
            if (std::holds_alternative<CharacterDef>(data) ||
                std::holds_alternative<SetCmd>(data) || std::holds_alternative<ConfigCmd>(data) ||
                std::holds_alternative<MetaCmd>(data) || std::holds_alternative<ScreenDef>(data)) {
                executeStatement(m_program.statements[i]);
            }
        }
        m_pc = startIdx + 1;
        m_currentLabel = "start";
    }

    const std::string osUser = core::system::userName();
    m_variables["sys.os_username"] = osUser;
    std::cout << "[ScriptVM] sys.os_username = \"" << osUser << "\"\n";

    std::cout << "[ScriptVM] Program loaded. " << m_characters.size() << " characters, "
              << m_variables.size() << " variables defined.\n";
}

void ScriptVM::advance() {
    if (m_vmState == VMState::WAITING_INPUT) {
        m_vmState = VMState::RUNNING;
        executeNext();
    }
}

void ScriptVM::update(double dt) {
    if (m_vmState == VMState::WAITING_TIMED) {
        m_waitTimer -= dt;
        if (m_waitTimer <= 0.0) {
            m_vmState = VMState::RUNNING;
            executeNext();
        }
    } else if (m_vmState == VMState::RUNNING) {
        executeNext();
    }
}

void ScriptVM::executeNext() {
    while (m_vmState == VMState::RUNNING && m_pc < m_program.statements.size()) {
        const auto& stmt = m_program.statements[m_pc];
        m_pc++;
        executeStatement(stmt);
        if (m_vmState != VMState::RUNNING)
            return;
    }
    if (m_pc >= m_program.statements.size()) {
        m_vmState = VMState::FINISHED;
        std::cout << "[ScriptVM] Script execution complete.\n";
    }
}

void ScriptVM::executeStatement(const Statement& stmt) {
    std::visit(
        [this](const auto& cmd) {
            using T = std::decay_t<decltype(cmd)>;
            if constexpr (std::is_same_v<T, CharacterDef>)
                execCharacterDef(cmd);
            else if constexpr (std::is_same_v<T, LabelStmt>)
                execLabel(cmd);
            else if constexpr (std::is_same_v<T, SceneCmd>)
                execScene(cmd);
            else if constexpr (std::is_same_v<T, ShowCmd>)
                execShow(cmd);
            else if constexpr (std::is_same_v<T, HideCmd>)
                execHide(cmd);
            else if constexpr (std::is_same_v<T, SayCmd>)
                execSay(cmd);
            else if constexpr (std::is_same_v<T, PlayCmd>)
                execPlay(cmd);
            else if constexpr (std::is_same_v<T, StopCmd>)
                execStop(cmd);
            else if constexpr (std::is_same_v<T, WaitCmd>)
                execWait(cmd);
            else if constexpr (std::is_same_v<T, SetCmd>)
                execSet(cmd);
            else if constexpr (std::is_same_v<T, JumpCmd>)
                execJump(cmd);
            else if constexpr (std::is_same_v<T, MenuCmd>)
                execMenu(cmd);
            else if constexpr (std::is_same_v<T, IfStmt>)
                execIf(cmd);
            else if constexpr (std::is_same_v<T, ConfigCmd>)
                execConfig(cmd);
            else if constexpr (std::is_same_v<T, TweenCmd>)
                execTween(cmd);
            else if constexpr (std::is_same_v<T, EmotionCmd>)
                execEmotion(cmd);
            else if constexpr (std::is_same_v<T, InputCmd>)
                execInput(cmd);
            else if constexpr (std::is_same_v<T, MetaCmd>)
                execMeta(cmd);
            else if constexpr (std::is_same_v<T, ParticleCmd>)
                execParticle(cmd);
            else if constexpr (std::is_same_v<T, ScreenDef>) {
                m_screens[cmd.name] = cmd;
                std::cout << "[ScriptVM] Registered screen: " << cmd.name << "\n";
            } else if constexpr (std::is_same_v<T, ScreenShowCmd>) {
                auto it = m_screens.find(cmd.screenName);
                if (it != m_screens.end()) {
                    m_activeScreens[cmd.screenName] = it->second;
                    std::cout << "[ScriptVM] Showing screen: " << cmd.screenName << "\n";
                } else {
                    std::cerr << "[ScriptVM] Screen not found: " << cmd.screenName << "\n";
                }
            } else if constexpr (std::is_same_v<T, ScreenHideCmd>) {
                m_activeScreens.erase(cmd.screenName);
                std::cout << "[ScriptVM] Hiding screen: " << cmd.screenName << "\n";
            }
        },
        stmt.data);
}

void ScriptVM::execCharacterDef(const CharacterDef& cmd) {
    m_characters[cmd.id] = {cmd.displayName, cmd.color};
    std::cout << "[ScriptVM] Character defined: " << cmd.id << " (\"" << cmd.displayName << "\", "
              << cmd.color << ")\n";
}

void ScriptVM::execLabel(const LabelStmt& cmd) {
    m_currentLabel = cmd.name;
    std::cout << "[ScriptVM] Entering label: " << cmd.name << "\n";
}

void ScriptVM::execScene(const SceneCmd& cmd) {
    std::cout << "[ScriptVM] Scene: " << cmd.imagePath;
    if (cmd.transition)
        std::cout << " with " << cmd.transition->name << "(" << cmd.transition->duration << "s)";
    std::cout << "\n";
    if (m_engine && m_engine->getRenderer()) {
        m_engine->getRenderer()->setBackground(cmd.imagePath, cmd.transition);
    }
}

void ScriptVM::execShow(const ShowCmd& cmd) {
    std::string resolvedPath = cmd.imagePath;
    bool isExpressionName = !cmd.imagePath.empty() &&
                            cmd.imagePath.find('/') == std::string::npos &&
                            cmd.imagePath.find('\\') == std::string::npos &&
                            cmd.imagePath.find('.') == std::string::npos;

    if (isExpressionName) {
        const std::string prefixes[] = {"images/sprites/", "sprites/"};
        const std::string exts[] = {".png", ".webp", ".jpg"};
        bool found = false;

        for (const auto& prefix : prefixes) {
            for (const auto& ext : exts) {
                std::string tryPath = prefix + cmd.target + "/" + cmd.imagePath + ext;
                if (m_engine && m_engine->getAssetManager() &&
                    m_engine->getAssetManager()->exists(tryPath)) {
                    resolvedPath = tryPath;
                    found = true;
                    break;
                }
            }
            if (found)
                break;
        }

        if (!found) {
            for (const auto& prefix : prefixes) {
                for (const auto& ext : exts) {
                    std::string tryPath = prefix + cmd.target + ext;
                    if (m_engine && m_engine->getAssetManager() &&
                        m_engine->getAssetManager()->exists(tryPath)) {
                        resolvedPath = tryPath;
                        found = true;
                        std::cout << "[ScriptVM] Expression '" << cmd.imagePath
                                  << "' not found for " << cmd.target
                                  << "; using base sprite: " << tryPath << "\n";
                        break;
                    }
                }
                if (found)
                    break;
            }
        }
    }

    std::cout << "[ScriptVM] Show: " << cmd.target << " \"" << resolvedPath << "\"";
    if (cmd.position)
        std::cout << " at " << cmd.position->preset;
    if (cmd.transition)
        std::cout << " with " << cmd.transition->name;
    if (cmd.zoom != 1.0f)
        std::cout << " zoom(" << cmd.zoom << ")";
    if (cmd.flipped)
        std::cout << " flipped";
    std::cout << "\n";
    if (m_engine && m_engine->getRenderer()) {
        m_engine->getRenderer()->showSprite(cmd.target, resolvedPath, cmd.position, cmd.transition,
                                            cmd.flipped, cmd.zorder, cmd.zoom, cmd.rotate);
    }
}

void ScriptVM::execHide(const HideCmd& cmd) {
    std::cout << "[ScriptVM] Hide: " << (cmd.hideAll ? "all" : cmd.target) << "\n";
    if (m_engine && m_engine->getRenderer()) {
        if (cmd.hideAll) {
            m_engine->getRenderer()->clearAllSprites(cmd.transition);
        } else {
            m_engine->getRenderer()->hideSprite(cmd.target, cmd.transition);
        }
    }
}

void ScriptVM::execSay(const SayCmd& cmd) {
    std::string speakerName = cmd.speaker;
    std::string color = "#FFFFFF";

    if (cmd.speaker.empty()) {
        speakerName = "";
        color = "#B0BEC5";
    } else {
        auto* ch = getCharacter(cmd.speaker);
        if (ch) {
            speakerName = ch->displayName;
            color = ch->color;
        }
    }

    bool replaying = m_engine && m_engine->isReplaying();

    if (!replaying) {
        if (cmd.speaker.empty())
            std::cout << "[ScriptVM] (narrator): \"" << cmd.text << "\"\n";
        else
            std::cout << "[ScriptVM] " << speakerName << ": \"" << cmd.text << "\"\n";
    }

    std::string displayText = interpolateVariables(cmd.text);

    if (m_engine && m_engine->getLocalization()) {
        auto* l10n = m_engine->getLocalization();
        displayText = l10n->translate(displayText, "say", cmd.speaker);
        if (!cmd.speaker.empty()) {
            std::string trName = l10n->translate(speakerName, "character", cmd.speaker);
            if (trName != speakerName)
                speakerName = trName;
        }
    }

    if (!replaying && m_engine && m_engine->getRichText()) {
        m_engine->getRichText()->beginDialogue(speakerName, color, displayText);
    }

    if (!replaying && m_engine) {
        auto& history = m_engine->getDialogueHistory();
        history.push_back({speakerName, color, displayText});
        while (history.size() > core::Engine::MAX_HISTORY) {
            history.erase(history.begin());
        }
    }

    if (!replaying && m_engine && m_engine->getStateManager()) {
        core::GameSnapshot snap;
        snap.scriptLine = m_pc > 0 ? m_pc - 1 : 0;
        snap.currentLabel = m_currentLabel;
        snap.currentSpeaker = speakerName;
        snap.currentText = displayText;
        snap.textComplete = false;
        snap.timestamp = static_cast<int64_t>(std::time(nullptr));
        snap.variables = m_variables;
        if (m_engine->getRenderer()) {
            snap.backgroundPath = m_engine->getRenderer()->getCurrentBackground();
            for (const auto& sprite : m_engine->getRenderer()->getSprites()) {
                snap.sprites.push_back({sprite.id, sprite.imagePath, sprite.x, sprite.y,
                                        sprite.alpha, sprite.scaleX, sprite.scaleY, sprite.rotation,
                                        sprite.zOrder, sprite.flipped});
            }
        }
        m_engine->getStateManager()->captureSnapshot(snap);
    }

    m_vmState = VMState::WAITING_INPUT;
}

void ScriptVM::execPlay(const PlayCmd& cmd) {
    if (m_engine && m_engine->isReplaying())
        return;
    std::cout << "[ScriptVM] Play " << cmd.channel << ": " << cmd.filePath << "\n";
    if (m_engine && m_engine->getAudioMixer() && m_engine->getAssetManager()) {
        const std::string path = m_engine->getAssetManager()->resolvePath(cmd.filePath);
        if (path.empty()) {
            std::cerr << "[ScriptVM] Audio asset not found: " << cmd.filePath << '\n';
            return;
        }
        m_engine->getAudioMixer()->play(cmd.channel, path, cmd.fadeIn.value_or(0.0),
                                        cmd.volume.value_or(1.0), cmd.crossfade.value_or(0.0));
    }
}

void ScriptVM::execStop(const StopCmd& cmd) {
    if (m_engine && m_engine->isReplaying())
        return;
    std::cout << "[ScriptVM] Stop " << cmd.channel << "\n";
    if (m_engine && m_engine->getAudioMixer()) {
        m_engine->getAudioMixer()->stop(cmd.channel, cmd.fadeOut.value_or(0.0));
    }
}

void ScriptVM::execWait(const WaitCmd& cmd) {
    std::cout << "[ScriptVM] Wait " << cmd.duration << "s\n";
    if (m_engine && m_engine->isReplaying())
        return;
    m_waitTimer = cmd.duration;
    m_vmState = VMState::WAITING_TIMED;
}

void ScriptVM::execSet(const SetCmd& cmd) {
    VarValue val = evaluateExpression(cmd.value);
    setVariable(cmd.variable, val);
    std::cout << "[ScriptVM] Set " << cmd.variable << " = ";
    std::visit([](const auto& v) { std::cout << v; }, val);
    std::cout << "\n";
}

void ScriptVM::execJump(const JumpCmd& cmd) {
    auto it = m_program.labelIndex.find(cmd.label);
    if (it == m_program.labelIndex.end()) {
        std::cerr << "[ScriptVM] ERROR: Label not found: " << cmd.label << "\n";
        return;
    }
    m_pc = it->second + 1;
    m_currentLabel = cmd.label;
    std::cout << "[ScriptVM] Jump to: " << cmd.label << "\n";
}

void ScriptVM::execMenu(const MenuCmd& cmd) {
    std::cout << "[ScriptVM] Menu with " << cmd.choices.size() << " choices:\n";
    std::vector<std::string> choiceTexts;
    for (size_t i = 0; i < cmd.choices.size(); ++i) {
        std::cout << "  [" << i << "] " << cmd.choices[i].text << "\n";
        choiceTexts.push_back(cmd.choices[i].text);
    }
    m_currentMenu = &cmd;
    m_vmState = VMState::WAITING_CHOICE;

    if (m_engine && m_engine->getRichText()) {
        m_engine->getRichText()->showMenu(choiceTexts);
    }
}

void ScriptVM::execIf(const IfStmt& cmd) {
    VarValue cond = evaluateExpression(cmd.condition);
    if (isTruthy(cond)) {
        for (const auto& s : cmd.thenBody)
            executeStatement(s);
        return;
    }
    for (const auto& [elifCond, elifBody] : cmd.elifBranches) {
        if (isTruthy(evaluateExpression(elifCond))) {
            for (const auto& s : elifBody)
                executeStatement(s);
            return;
        }
    }
    for (const auto& s : cmd.elseBody)
        executeStatement(s);
}

void ScriptVM::execConfig(const ConfigCmd& cmd) {
    VarValue val = evaluateExpression(cmd.value);
    std::cout << "[ScriptVM] Config: " << cmd.key << " = ";
    std::visit([](const auto& v) { std::cout << v; }, val);
    std::cout << "\n";
    setVariable("__config." + cmd.key, val);
}

void ScriptVM::execTween(const TweenCmd& cmd) {
    if (!m_engine || !m_engine->getRenderer() || !m_engine->getTweenManager())
        return;
    auto from = m_engine->getRenderer()->getSpriteProperty(cmd.target, cmd.property);
    if (!from) {
        std::cerr << "[ScriptVM] Unknown tween target or property: " << cmd.target << '.'
                  << cmd.property << '\n';
        return;
    }

    const VarValue evaluated = evaluateExpression(cmd.toValue);
    float to = *from;
    try {
        to = std::visit(
            [](const auto& value) -> float {
                using T = std::decay_t<decltype(value)>;
                if constexpr (std::is_same_v<T, double>)
                    return static_cast<float>(value);
                else if constexpr (std::is_same_v<T, bool>)
                    return value ? 1.0f : 0.0f;
                else
                    return std::stof(value);
            },
            evaluated);
    } catch (const std::exception&) {
        std::cerr << "[ScriptVM] Tween value must be numeric\n";
        return;
    }

    auto* renderer = m_engine->getRenderer();
    auto* tweens = m_engine->getTweenManager();
    tweens->cancelAllFor(cmd.target);
    const auto id =
        tweens->createTween(cmd.target, cmd.property, *from, to,
                            static_cast<float>(std::max(0.001, cmd.duration)), cmd.easing);
    tweens->setOnUpdate(id, [renderer, target = cmd.target, property = cmd.property](float value) {
        renderer->setSpriteProperty(target, property, value);
    });
}

void ScriptVM::execEmotion(const EmotionCmd& cmd) {
    if (!m_engine || !m_engine->getRenderer() || !m_engine->getTweenManager())
        return;
    auto* renderer = m_engine->getRenderer();
    auto* tweens = m_engine->getTweenManager();

    std::string property;
    float delta = 0.0f;
    float duration = 0.2f;
    if (cmd.emotion == "happy") {
        property = "y";
        delta = -0.025f;
    } else if (cmd.emotion == "sad") {
        property = "alpha";
        delta = -0.2f;
        duration = 0.6f;
    } else if (cmd.emotion == "surprised") {
        property = "scale";
        delta = 0.08f;
    } else if (cmd.emotion == "angry" || cmd.emotion == "nervous") {
        renderer->shakeScreen(cmd.emotion == "angry" ? 8.0f : 3.0f, 0.25f);
        return;
    } else {
        std::cerr << "[ScriptVM] Unknown emotion: " << cmd.emotion << '\n';
        return;
    }

    const auto from = renderer->getSpriteProperty(cmd.character, property);
    if (!from)
        return;
    tweens->cancelAllFor(cmd.character);
    const auto id =
        tweens->createTween(cmd.character, property, *from, *from + delta, duration, "ease_out");
    tweens->setOnUpdate(id, [renderer, target = cmd.character, property](float value) {
        renderer->setSpriteProperty(target, property, value);
    });
    tweens->setOnComplete(id, [renderer, tweens, target = cmd.character, property, start = *from,
                               end = *from + delta, duration]() {
        const auto back =
            tweens->createTween(target, property, end, start, duration, "ease_in_out");
        tweens->setOnUpdate(back, [renderer, target, property](float value) {
            renderer->setSpriteProperty(target, property, value);
        });
    });
}

void ScriptVM::selectChoice(int index) {
    if (m_vmState != VMState::WAITING_CHOICE)
        return;

    if (!m_textInputOptions.empty() && !m_pendingInputVar.empty()) {
        if (index >= 0 && index < static_cast<int>(m_textInputOptions.size()))
            submitTextInput(m_textInputOptions[index]);
        return;
    }

    if (!m_currentMenu)
        return;
    if (index < 0 || index >= static_cast<int>(m_currentMenu->choices.size()))
        return;

    const auto& choice = m_currentMenu->choices[index];
    std::cout << "[ScriptVM] Player chose: \"" << choice.text << "\"\n";

    for (const auto& s : choice.body)
        executeStatement(s);

    m_currentMenu = nullptr;
    m_vmState = VMState::RUNNING;
}

void ScriptVM::setVariable(const std::string& name, VarValue value) {
    m_variables[name] = std::move(value);
}

VarValue ScriptVM::getVariable(const std::string& name) const {
    auto it = m_variables.find(name);
    if (it != m_variables.end())
        return it->second;
    return std::string("");
}

bool ScriptVM::hasVariable(const std::string& name) const {
    return m_variables.count(name) > 0;
}

const ScriptVM::CharacterInfo* ScriptVM::getCharacter(const std::string& id) const {
    auto it = m_characters.find(id);
    if (it != m_characters.end())
        return &it->second;
    return nullptr;
}

const ScriptVM::CharacterInfo* ScriptVM::getCharacterByDisplayName(const std::string& name) const {
    for (const auto& [id, info] : m_characters) {
        if (info.displayName == name)
            return &info;
    }
    return nullptr;
}

VarValue ScriptVM::evaluateExpression(const Expression& expr) const {
    switch (expr.type) {
    case Expression::Type::STRING:
        return expr.value;
    case Expression::Type::NUMBER:
        return std::stod(expr.value);
    case Expression::Type::BOOLEAN:
        return expr.value == "true";
    case Expression::Type::COLOR:
        return expr.value;
    case Expression::Type::IDENTIFIER:
    case Expression::Type::VARIABLE_REF: {
        if (expr.value == "os_username") {
            const std::string name = core::system::userName();
            std::cout << "[ScriptVM] Builtin os_username = \"" << name << "\"\n";
            return name;
        }
        if (expr.value == "os_time") {
            const auto formatted = core::system::formatLocalTime(std::time(nullptr), "%H:%M");
            return formatted.value_or("");
        }
        if (expr.value == "os_locale") {
            return core::system::preferredLocale();
        }
        return getVariable(expr.value);
    }
    case Expression::Type::UNARY_OP: {
        if (expr.children.empty())
            return false;
        const VarValue value = evaluateExpression(expr.children.front());
        if (expr.op == "not")
            return !isTruthy(value);
        if (expr.op == "-") {
            return std::visit(
                [](const auto& item) -> double {
                    using T = std::decay_t<decltype(item)>;
                    if constexpr (std::is_same_v<T, double>)
                        return -item;
                    if constexpr (std::is_same_v<T, bool>)
                        return item ? -1.0 : 0.0;
                    if constexpr (std::is_same_v<T, std::string>) {
                        try {
                            return -std::stod(item);
                        } catch (...) {
                            return 0.0;
                        }
                    }
                },
                value);
        }
        return false;
    }
    case Expression::Type::BINARY_OP: {
        if (expr.children.size() < 2)
            return false;
        VarValue lhs = evaluateExpression(expr.children[0]);
        if (expr.op == "and" && !isTruthy(lhs))
            return false;
        if (expr.op == "or" && isTruthy(lhs))
            return true;
        VarValue rhs = evaluateExpression(expr.children[1]);

        auto toString = [](const VarValue& v) -> std::string {
            return std::visit(
                [](const auto& val) -> std::string {
                    using T = std::decay_t<decltype(val)>;
                    if constexpr (std::is_same_v<T, std::string>)
                        return val;
                    else if constexpr (std::is_same_v<T, double>)
                        return std::to_string(val);
                    else if constexpr (std::is_same_v<T, bool>)
                        return val ? "true" : "false";
                    return "";
                },
                v);
        };

        auto toDouble = [](const VarValue& v) -> double {
            return std::visit(
                [](const auto& val) -> double {
                    using T = std::decay_t<decltype(val)>;
                    if constexpr (std::is_same_v<T, double>)
                        return val;
                    else if constexpr (std::is_same_v<T, bool>)
                        return val ? 1.0 : 0.0;
                    else if constexpr (std::is_same_v<T, std::string>) {
                        try {
                            return std::stod(val);
                        } catch (...) {
                            return 0.0;
                        }
                    }
                    return 0.0;
                },
                v);
        };

        if (expr.op == "==")
            return toString(lhs) == toString(rhs);
        if (expr.op == "!=")
            return toString(lhs) != toString(rhs);
        if (expr.op == "<")
            return toDouble(lhs) < toDouble(rhs);
        if (expr.op == ">")
            return toDouble(lhs) > toDouble(rhs);
        if (expr.op == "<=")
            return toDouble(lhs) <= toDouble(rhs);
        if (expr.op == ">=")
            return toDouble(lhs) >= toDouble(rhs);
        if (expr.op == "and")
            return isTruthy(rhs);
        if (expr.op == "or")
            return isTruthy(rhs);

        if (expr.op == "+")
            return toDouble(lhs) + toDouble(rhs);
        if (expr.op == "-")
            return toDouble(lhs) - toDouble(rhs);
        if (expr.op == "*")
            return toDouble(lhs) * toDouble(rhs);
        if (expr.op == "/") {
            double d = toDouble(rhs);
            return d != 0.0 ? toDouble(lhs) / d : 0.0;
        }
        return false;
    }
    default:
        return std::string("");
    }
}

bool ScriptVM::isTruthy(const VarValue& val) const {
    return std::visit(
        [](const auto& v) -> bool {
            using T = std::decay_t<decltype(v)>;
            if constexpr (std::is_same_v<T, bool>)
                return v;
            else if constexpr (std::is_same_v<T, double>)
                return v != 0.0;
            else if constexpr (std::is_same_v<T, std::string>)
                return !v.empty() && v != "false";
            return false;
        },
        val);
}

std::string ScriptVM::interpolateVariables(const std::string& text) const {
    std::string result;
    result.reserve(text.size());

    for (size_t i = 0; i < text.size(); ++i) {
        if (text[i] == '[') {
            size_t close = text.find(']', i + 1);
            if (close != std::string::npos) {
                std::string varName = text.substr(i + 1, close - i - 1);
                if (hasVariable(varName)) {
                    VarValue val = getVariable(varName);
                    std::visit(
                        [&result](const auto& v) {
                            using T = std::decay_t<decltype(v)>;
                            if constexpr (std::is_same_v<T, std::string>)
                                result += v;
                            else if constexpr (std::is_same_v<T, double>) {
                                if (v == static_cast<int>(v))
                                    result += std::to_string(static_cast<int>(v));
                                else
                                    result += std::to_string(v);
                            } else if constexpr (std::is_same_v<T, bool>)
                                result += v ? "true" : "false";
                        },
                        val);
                    i = close;
                    continue;
                }
            }
        }
        result += text[i];
    }
    return result;
}

void ScriptVM::execInput(const InputCmd& cmd) {
    std::cout << "[ScriptVM] Input: prompt=\"" << cmd.prompt << "\" -> " << cmd.variable
              << " type=" << cmd.inputType << "\n";

    m_pendingInputVar = cmd.variable;
    m_textInputPrompt = cmd.prompt;
    m_textInputOptions = cmd.options;

    if (cmd.inputType == "choice" && !cmd.options.empty()) {
        std::cout << "[ScriptVM] Input choice with " << cmd.options.size() << " options:\n";
        for (size_t i = 0; i < cmd.options.size(); ++i) {
            std::cout << "  [" << i << "] " << cmd.options[i] << "\n";
        }
        if (m_engine && m_engine->getRichText()) {
            m_engine->getRichText()->showMenu(cmd.options);
        }
        m_vmState = VMState::WAITING_CHOICE;
    } else {
        m_vmState = VMState::WAITING_TEXT_INPUT;
        std::cout << "[ScriptVM] Waiting for text input...\n";
    }
}

void ScriptVM::submitTextInput(const std::string& text) {
    if (m_vmState != VMState::WAITING_TEXT_INPUT && m_vmState != VMState::WAITING_CHOICE)
        return;

    std::cout << "[ScriptVM] Text input received: \"" << text << "\" -> " << m_pendingInputVar
              << "\n";

    setVariable(m_pendingInputVar, text);

    if (!text.empty()) {
        std::vector<uint32_t> cps;
        for (size_t i = 0; i < text.size();) {
            uint32_t cp = 0;
            auto b = static_cast<unsigned char>(text[i]);
            if (b < 0x80) {
                cp = b;
                i += 1;
            } else if (b < 0xE0 && i + 1 < text.size()) {
                cp = (b & 0x1F) << 6 | (static_cast<unsigned char>(text[i + 1]) & 0x3F);
                i += 2;
            } else if (b < 0xF0 && i + 2 < text.size()) {
                cp = (b & 0x0F) << 12 | (static_cast<unsigned char>(text[i + 1]) & 0x3F) << 6 |
                     (static_cast<unsigned char>(text[i + 2]) & 0x3F);
                i += 3;
            } else if (b < 0xF8 && i + 3 < text.size()) {
                cp = (b & 0x07) << 18 | (static_cast<unsigned char>(text[i + 1]) & 0x3F) << 12 |
                     (static_cast<unsigned char>(text[i + 2]) & 0x3F) << 6 |
                     (static_cast<unsigned char>(text[i + 3]) & 0x3F);
                i += 4;
            } else {
                cp = 0xFFFD;
                ++i;
            }
            cps.push_back(cp);
        }

        auto toLowerCP = [](uint32_t cp) -> uint32_t {
            if (cp >= 'A' && cp <= 'Z')
                return cp + 32;
            if (cp >= 0x410 && cp <= 0x42F)
                return cp + 0x20;
            if (cp == 0x401)
                return 0x451;
            return cp;
        };

        auto isVowel = [&](uint32_t cp) -> bool {
            cp = toLowerCP(cp);
            if (cp == 'a' || cp == 'e' || cp == 'i' || cp == 'o' || cp == 'u' || cp == 'y')
                return true;
            if (cp == 0x430 || cp == 0x435 || cp == 0x451 || cp == 0x438 || cp == 0x43E ||
                cp == 0x443 || cp == 0x44B || cp == 0x44D || cp == 0x44E || cp == 0x44F)
                return true;
            return false;
        };

        auto isLetter = [&](uint32_t cp) -> bool {
            cp = toLowerCP(cp);
            if (cp >= 'a' && cp <= 'z')
                return true;
            if (cp >= 0x430 && cp <= 0x44F)
                return true;
            if (cp == 0x451)
                return true;
            return false;
        };

        int maxConsConsonants = 0, curConsonants = 0;
        bool hasVowel = false;
        int sameCharCount = 1, maxSameChar = 1;

        for (size_t i = 0; i < cps.size(); i++) {
            uint32_t cp = cps[i];
            if (isLetter(cp)) {
                if (isVowel(cp)) {
                    hasVowel = true;
                    curConsonants = 0;
                } else {
                    curConsonants++;
                    maxConsConsonants = (std::max)(maxConsConsonants, curConsonants);
                }
            }
            if (i > 0 && toLowerCP(cps[i]) == toLowerCP(cps[i - 1])) {
                sameCharCount++;
                maxSameChar = (std::max)(maxSameChar, sameCharCount);
            } else {
                sameCharCount = 1;
            }
        }

        int letterCount = 0;
        for (auto cp : cps)
            if (isLetter(cp))
                letterCount++;

        bool gibberish =
            (letterCount > 2 && !hasVowel) || (maxConsConsonants >= 4) || (maxSameChar >= 4);
        setVariable("sys.name_gibberish", gibberish);
        std::cout << "[ScriptVM] sys.name_gibberish = " << (gibberish ? "true" : "false")
                  << " (consec_cons=" << maxConsConsonants << " same=" << maxSameChar
                  << " vowel=" << (hasVowel ? "yes" : "no") << ")\n";
    }

    m_pendingInputVar.clear();
    m_textInputPrompt.clear();
    m_textInputOptions.clear();
    m_vmState = VMState::RUNNING;
}

void ScriptVM::execMeta(const MetaCmd& cmd) {
    std::cout << "[ScriptVM] Meta: " << cmd.action;
    for (const auto& a : cmd.args)
        std::cout << " " << a;
    std::cout << "\n";

    if (!m_engine)
        return;

    if (m_engine->isReplaying() &&
        (cmd.action == "file_write" || cmd.action == "file_delete" || cmd.action == "delete_save" ||
         cmd.action == "quit" || cmd.action == "exit" || cmd.action == "main_menu")) {
        return;
    }

    const auto numberArg = [&cmd](size_t index, double fallback) {
        if (index >= cmd.args.size())
            return fallback;
        try {
            return std::stod(cmd.args[index]);
        } catch (...) {
            return fallback;
        }
    };

    if (cmd.action == "set_title") {
        if (!cmd.args.empty()) {
            SDL_Window* win = m_engine->getWindow();
            if (win) {
                SDL_SetWindowTitle(win, cmd.args[0].c_str());
                std::cout << "[ScriptVM] Meta: window title -> \"" << cmd.args[0] << "\"\n";
            }
        }
    } else if (cmd.action == "lock_save") {
        m_engine->setSaveLocked(true);
        std::cout << "[ScriptVM] Meta: saving locked\n";
    } else if (cmd.action == "unlock_save") {
        m_engine->setSaveLocked(false);
        std::cout << "[ScriptVM] Meta: saving unlocked\n";
    } else if (cmd.action == "glitch_text") {
        double dur = numberArg(0, 1.0);
        m_engine->startGlitchText(dur);
        std::cout << "[ScriptVM] Meta: text glitch for " << dur << "s\n";
    } else if (cmd.action == "glitch_ui") {
        double dur = numberArg(0, 1.0);
        m_engine->startGlitchUI(dur);
        std::cout << "[ScriptVM] Meta: UI glitch for " << dur << "s\n";
    } else if (cmd.action == "shake_window") {
        double dur = numberArg(0, 0.3);
        m_engine->startWindowShake(dur);
        std::cout << "[ScriptVM] Meta: window shake for " << dur << "s\n";
    } else if (cmd.action == "hide_quickbar") {
        m_engine->setQuickbarVisible(false);
        std::cout << "[ScriptVM] Meta: quickbar hidden\n";
    } else if (cmd.action == "show_quickbar") {
        m_engine->setQuickbarVisible(true);
        std::cout << "[ScriptVM] Meta: quickbar shown\n";
    } else if (cmd.action == "delete_save") {
        int slot = static_cast<int>(numberArg(0, 0.0));
        if (m_engine->getStateManager()) {
            m_engine->getStateManager()->deleteSave(slot);
            std::cout << "[ScriptVM] Meta: deleted save slot " << slot << "\n";
        }
    } else if (cmd.action == "fullscreen") {
        SDL_Window* win = m_engine->getWindow();
        if (win)
            SDL_SetWindowFullscreen(win, true);
    } else if (cmd.action == "windowed") {
        SDL_Window* win = m_engine->getWindow();
        if (win)
            SDL_SetWindowFullscreen(win, false);
    } else if (cmd.action == "file_write") {
        if (cmd.args.size() >= 2) {
            try {
                // Rejects absolute paths and parent traversal.
                const auto path = core::paths::scriptDataPath(cmd.args[0], true);
                if (!path)
                    throw std::runtime_error("path must stay inside script-data");
                std::ofstream ofs(*path, std::ios::out | std::ios::trunc);
                if (ofs) {
                    ofs << cmd.args[1];
                    ofs.close();
                    std::cout << "[ScriptVM] Meta: wrote script data " << *path << "\n";
                } else {
                    std::cerr << "[ScriptVM] Meta: could not write file: " << cmd.args[0] << "\n";
                }
            } catch (...) {
                std::cerr << "[ScriptVM] Meta: file write failed: " << cmd.args[0] << "\n";
            }
        }
    } else if (cmd.action == "file_delete") {
        if (!cmd.args.empty()) {
            try {
                const auto path = core::paths::scriptDataPath(cmd.args[0]);
                if (!path)
                    throw std::runtime_error("path must stay inside script-data");
                if (std::filesystem::is_regular_file(*path)) {
                    std::filesystem::remove(*path);
                    std::cout << "[ScriptVM] Meta: deleted script data " << *path << "\n";
                } else {
                    std::cout << "[ScriptVM] Meta: file does not exist: " << cmd.args[0] << "\n";
                }
            } catch (...) {
                std::cerr << "[ScriptVM] Meta: file deletion failed\n";
            }
        }
    } else if (cmd.action == "quit" || cmd.action == "exit") {
        std::cout << "[ScriptVM] Meta: quitting\n";
        m_engine->requestQuit();
    } else if (cmd.action == "main_menu") {
        std::cout << "[ScriptVM] Meta: returning to main menu\n";
        m_engine->requestStateChange(core::EngineState::MAIN_MENU);
    } else if (cmd.action == "effect") {
        if (cmd.args.empty())
            return;
        auto* postFX = m_engine->getPostEffects();
        if (!postFX)
            return;

        std::string fxName = cmd.args[0];
        if (fxName == "clear") {
            postFX->clearAll();
            std::cout << "[ScriptVM] Meta: cleared all effects\n";
        } else {
            float intensity = static_cast<float>(std::clamp(numberArg(1, 1.0), 0.0, 1.0));
            float duration = static_cast<float>(std::max(numberArg(2, 0.0), 0.0));

            core::PostEffectType type;
            if (fxName == "blur")
                type = core::PostEffectType::BLUR;
            else if (fxName == "vignette")
                type = core::PostEffectType::VIGNETTE;
            else if (fxName == "crt")
                type = core::PostEffectType::CRT;
            else if (fxName == "glitch")
                type = core::PostEffectType::GLITCH;
            else if (fxName == "fade")
                type = core::PostEffectType::FADE;
            else if (fxName == "color_grade")
                type = core::PostEffectType::COLOR_GRADE;
            else {
                std::cerr << "[ScriptVM] Meta effect: unknown type '" << fxName << "'\n";
                return;
            }

            postFX->setEffect(type, intensity, duration);

            if ((type == core::PostEffectType::FADE || type == core::PostEffectType::COLOR_GRADE) &&
                cmd.args.size() >= 6) {
                float r = static_cast<float>(std::clamp(numberArg(3, 0.0), 0.0, 1.0));
                float g = static_cast<float>(std::clamp(numberArg(4, 0.0), 0.0, 1.0));
                float b = static_cast<float>(std::clamp(numberArg(5, 0.0), 0.0, 1.0));
                postFX->setEffectColor(type, r, g, b);
            }
            std::cout << "[ScriptVM] Meta: effect " << fxName << " intensity=" << intensity
                      << " duration=" << duration << "\n";
        }
    } else {
        std::cerr << "[ScriptVM] Meta: unknown command '" << cmd.action << "'\n";
    }
}

void ScriptVM::execParticle(const ParticleCmd& cmd) {
    if (!m_engine || !m_engine->getParticles())
        return;
    if (m_engine->isReplaying())
        return;
    auto* ps = m_engine->getParticles();

    if (cmd.preset == "stop") {
        if (cmd.stopAll) {
            ps->stopAll();
        } else if (!cmd.name.empty()) {
            ps->stopEmitter(cmd.name);
        }
        std::cout << "[ScriptVM] Particle stop " << (cmd.stopAll ? "all" : cmd.name) << "\n";
        return;
    }

    static const std::unordered_map<std::string, render::ParticlePreset> presets = {
        {"rain", render::ParticlePreset::RAIN},     {"snow", render::ParticlePreset::SNOW},
        {"fire", render::ParticlePreset::FIRE},     {"sparkle", render::ParticlePreset::SPARKLE},
        {"dust", render::ParticlePreset::DUST},     {"petals", render::ParticlePreset::PETALS},
        {"custom", render::ParticlePreset::CUSTOM},
    };

    auto it = presets.find(cmd.preset);
    if (it == presets.end()) {
        std::cerr << "[ScriptVM] Unknown particle preset: " << cmd.preset << "\n";
        return;
    }

    auto emitter = render::ParticleSystem::makePreset(it->second, cmd.intensity, cmd.wind);
    if (!cmd.name.empty())
        emitter.name = cmd.name;
    if (it->second == render::ParticlePreset::FIRE) {
        emitter.areaX = cmd.x - cmd.radius;
        emitter.areaY = cmd.y - cmd.radius;
        emitter.areaW = cmd.radius * 2;
        emitter.areaH = cmd.radius * 2;
    }
    ps->addEmitter(emitter);
    std::cout << "[ScriptVM] Particle " << cmd.preset << " intensity=" << cmd.intensity
              << " wind=" << cmd.wind << "\n";
}
} // namespace velora::script
