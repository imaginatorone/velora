
#pragma once

#include "Parser.h"
#include <string>
#include <unordered_map>
#include <variant>
#include <vector>
#include <functional>

namespace velora::core {
class Engine;
}

namespace velora::script {

using VarValue = std::variant<std::string, double, bool>;

enum class VMState {
    RUNNING,
    WAITING_INPUT,
    WAITING_CHOICE,
    WAITING_TIMED,
    WAITING_TEXT_INPUT,
    FINISHED,
};

class ScriptVM {
  public:
    explicit ScriptVM(velora::core::Engine* engine);
    ~ScriptVM();

    void loadProgram(Program program);

    void advance();

    void update(double dt);

    [[nodiscard]] VMState getState() const { return m_vmState; }

    void setVariable(const std::string& name, VarValue value);
    VarValue getVariable(const std::string& name) const;
    bool hasVariable(const std::string& name) const;

    struct CharacterInfo {
        std::string displayName;
        std::string color;
    };
    [[nodiscard]] const CharacterInfo* getCharacter(const std::string& id) const;

    [[nodiscard]] const CharacterInfo* getCharacterByDisplayName(const std::string& name) const;

    [[nodiscard]] size_t getCurrentLine() const { return m_pc; }
    [[nodiscard]] const std::string& getCurrentLabel() const { return m_currentLabel; }
    void setCurrentLine(size_t line) { m_pc = line; }

    void forceState(VMState state) { m_vmState = state; }

    void selectChoice(int index);

    void submitTextInput(const std::string& text);

    [[nodiscard]] const std::string& getTextInputPrompt() const { return m_textInputPrompt; }
    [[nodiscard]] const std::vector<std::string>& getTextInputOptions() const {
        return m_textInputOptions;
    }

  private:
    void executeNext();
    void executeStatement(const Statement& stmt);

    void execCharacterDef(const CharacterDef& cmd);
    void execLabel(const LabelStmt& cmd);
    void execScene(const SceneCmd& cmd);
    void execShow(const ShowCmd& cmd);
    void execHide(const HideCmd& cmd);
    void execSay(const SayCmd& cmd);
    void execPlay(const PlayCmd& cmd);
    void execStop(const StopCmd& cmd);
    void execWait(const WaitCmd& cmd);
    void execSet(const SetCmd& cmd);
    void execJump(const JumpCmd& cmd);
    void execMenu(const MenuCmd& cmd);
    void execIf(const IfStmt& cmd);
    void execConfig(const ConfigCmd& cmd);
    void execTween(const TweenCmd& cmd);
    void execEmotion(const EmotionCmd& cmd);
    void execInput(const InputCmd& cmd);
    void execMeta(const MetaCmd& cmd);
    void execParticle(const ParticleCmd& cmd);

    VarValue evaluateExpression(const Expression& expr) const;
    bool isTruthy(const VarValue& val) const;

    velora::core::Engine* m_engine = nullptr;
    Program m_program;
    size_t m_pc = 0;
    VMState m_vmState = VMState::FINISHED;
    double m_waitTimer = 0.0;
    std::string m_currentLabel;

    std::unordered_map<std::string, VarValue> m_variables;
    std::unordered_map<std::string, CharacterInfo> m_characters;

    const MenuCmd* m_currentMenu = nullptr;

    std::string m_pendingInputVar;
    std::string m_textInputPrompt;
    std::vector<std::string> m_textInputOptions;

    std::string interpolateVariables(const std::string& text) const;

    std::unordered_map<std::string, ScreenDef> m_screens;
    std::unordered_map<std::string, ScreenDef> m_activeScreens;

  public:
    [[nodiscard]] const auto& getActiveScreens() const { return m_activeScreens; }
};

} // namespace velora::script
