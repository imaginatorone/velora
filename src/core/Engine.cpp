
#include "Engine.h"
#include "TimeManager.h"
#include "GameStateManager.h"
#include "script/Lexer.h"
#include "script/Parser.h"
#include "script/ScriptVM.h"
#include "render/Renderer.h"
#include "render/TweenManager.h"
#include "render/ParticleSystem.h"
#include "audio/AudioMixer.h"
#include "text/RichTextParser.h"
#include "asset/AssetManager.h"
#include "i18n/LocalizationManager.h"
#include "Paths.h"
#include "SystemInfo.h"

#include <SDL3/SDL.h>
#include <SDL3_image/SDL_image.h>
#include <SDL3_ttf/SDL_ttf.h>

#include <iostream>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <algorithm>
#include <cstring>
#include <nlohmann/json.hpp>

#ifndef VELORA_VERSION
#define VELORA_VERSION "0.2.0"
#endif

namespace velora::core {

Engine::Engine() = default;
Engine::~Engine() {
    shutdown();
}

bool Engine::initialize(const EngineConfig& config) {
    m_config = config;
    m_state = EngineState::INITIALIZING;
    std::cout << "[Engine] Initializing subsystems...\n";

    if (!SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_EVENTS)) {
        std::cerr << "[Engine] SDL_Init failed: " << SDL_GetError() << "\n";
        return false;
    }
    m_sdlInitialized = true;
    if (!TTF_Init()) {
        std::cerr << "[Engine] TTF_Init failed: " << SDL_GetError() << "\n";
        return false;
    }
    m_ttfInitialized = true;

    const std::filesystem::path userData = paths::userDataDirectory();
    m_config.assetRoot = paths::resourcePath(config.assetRoot).string();
    m_config.scriptPath = paths::resourcePath(config.scriptPath).string();

    m_settings = std::make_unique<SettingsManager>(userData / "settings.json");
    m_settings->load();
    m_displayMode = m_settings->displayMode;
    m_isFullscreen = m_settings->isFullscreen;
    m_locale = m_settings->locale;
    m_musicVolume = m_settings->musicVolume;
    m_soundVolume = m_settings->soundVolume;
    m_textSpeed = m_settings->textSpeed;
    m_autoDelay = m_settings->autoDelay;
    m_skipSpeed = m_settings->skipSpeed;
    m_skipEffects = m_settings->skipEffects;
    if (!m_config.overrideWindowSize) {
        m_config.windowWidth = m_settings->windowWidth;
        m_config.windowHeight = m_settings->windowHeight;
    }

    Uint64 wf = SDL_WINDOW_RESIZABLE | SDL_WINDOW_HIGH_PIXEL_DENSITY;
    if (m_config.fullscreen || m_isFullscreen || m_displayMode == 2)
        wf |= SDL_WINDOW_FULLSCREEN;
    if (m_displayMode == 1)
        wf |= SDL_WINDOW_BORDERLESS;
    m_window = SDL_CreateWindow(m_config.windowTitle.c_str(), m_config.windowWidth,
                                m_config.windowHeight, wf);
    if (!m_window) {
        std::cerr << "[Engine] Window failed: " << SDL_GetError() << "\n";
        return false;
    }

    {
        const auto iconPath = paths::resourcePath("assets/icons/velora.svg");
        SDL_Surface* icon = IMG_Load(iconPath.string().c_str());
        if (icon) {
            SDL_SetWindowIcon(m_window, icon);
            SDL_DestroySurface(icon);
            std::cout << "[Engine] Window icon set.\n";
        }
    }

    m_sdlRenderer = SDL_CreateRenderer(m_window, nullptr);
    if (!m_sdlRenderer) {
        std::cerr << "[Engine] Renderer failed: " << SDL_GetError() << "\n";
        return false;
    }
    SDL_SetRenderVSync(m_sdlRenderer, 1);
    // UI coordinates stay stable while SDL letterboxes to the window size.
    SDL_SetRenderLogicalPresentation(m_sdlRenderer, 1280, 720, SDL_LOGICAL_PRESENTATION_LETTERBOX);

    m_timeManager = std::make_unique<TimeManager>(m_settings->targetFPS);
    m_assetManager = std::make_unique<asset::AssetManager>(m_config.assetRoot);
    m_audioMixer = std::make_unique<audio::AudioMixer>();
    if (!m_audioMixer->initialize()) {
        std::cerr << "[Engine] Audio is unavailable; continuing without sound.\n";
    }
    m_renderer = std::make_unique<render::Renderer>(m_sdlRenderer, m_assetManager.get());
    m_tweenManager = std::make_unique<render::TweenManager>();
    m_particles = std::make_unique<render::ParticleSystem>();
    m_postEffects = std::make_unique<PostEffects>();
    if (!m_postEffects->initialize(m_sdlRenderer, DESIGN_W, DESIGN_H))
        m_postEffects.reset();
    const std::filesystem::path localesDir = std::filesystem::path(m_config.assetRoot) / "locales";
    m_localization = std::make_unique<i18n::LocalizationManager>("en", localesDir);
    m_localization->detectAvailableLanguages(localesDir.string());
    m_localization->loadTranslations((localesDir / "en.json").string());
    if (m_locale != "en")
        m_localization->setLocale(m_locale);
    m_richText = std::make_unique<text::RichTextParser>(m_config.assetRoot);
    m_stateManager = std::make_unique<GameStateManager>(userData / "saves");
    m_stateManager->loadPersistent();
    m_scriptVM = std::make_unique<script::ScriptVM>(this);

    m_richText->setVariableResolver([this](const std::string& name) {
        if (!m_scriptVM)
            return std::string{};
        const auto value = m_scriptVM->getVariable(name);
        return std::visit(
            [](const auto& item) -> std::string {
                using T = std::decay_t<decltype(item)>;
                if constexpr (std::is_same_v<T, std::string>)
                    return item;
                else if constexpr (std::is_same_v<T, bool>)
                    return item ? "true" : "false";
                else
                    return std::to_string(item);
            },
            value);
    });
    m_richText->setTagCallback([this](const std::string& command, const std::string& argument) {
        try {
            if (command == "shake_screen" && m_renderer) {
                m_renderer->shakeScreen(10.0f, std::stof(argument));
            } else if (command == "play_sfx" && m_audioMixer && m_assetManager) {
                const std::string path = m_assetManager->resolvePath(argument);
                if (!path.empty())
                    m_audioMixer->playSFX(path);
            } else if (command == "voice" && m_audioMixer && m_assetManager) {
                const std::string path = m_assetManager->resolvePath(argument);
                if (!path.empty())
                    m_audioMixer->play("voice", path);
            }
        } catch (const std::exception& error) {
            std::cerr << "[RichText] Invalid inline tag {" << command << ':' << argument
                      << "}: " << error.what() << '\n';
        }
    });

    if (m_richText)
        m_richText->setCharactersPerSecond(m_textSpeed);
    if (m_audioMixer) {
        m_audioMixer->setBGMVolume(m_musicVolume);
        m_audioMixer->setSFXVolume(m_soundVolume);
    }

    {
        const std::filesystem::path fontPaths[] = {
            std::filesystem::path(m_config.assetRoot) / "fonts/default.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "C:/Windows/Fonts/segoeui.ttf",
            "C:/Windows/Fonts/arial.ttf"};
        std::filesystem::path foundPath;
        for (const auto& path : fontPaths) {
            if (std::filesystem::exists(path)) {
                foundPath = path;
                break;
            }
        }
        if (!foundPath.empty()) {
            m_fontTitle = TTF_OpenFont(foundPath.string().c_str(), 42.0f);
            m_fontMenu = TTF_OpenFont(foundPath.string().c_str(), 28.0f);
            m_fontSub = TTF_OpenFont(foundPath.string().c_str(), 20.0f);
            m_fontSmall = TTF_OpenFont(foundPath.string().c_str(), 16.0f);
            std::cout << "[Engine] UI fonts loaded: " << foundPath << " (42/28/20/16px)\n";
        }
    }

    if (!m_config.scriptPath.empty()) {
        std::filesystem::path sf(m_config.scriptPath);
        if (std::filesystem::exists(sf)) {
            std::ifstream file(sf);
            std::stringstream buf;
            buf << file.rdbuf();
            try {
                script::Lexer lexer(buf.str(), m_config.scriptPath);
                auto tokens = lexer.tokenize();
                std::cout << "[Engine] Lexer: " << tokens.size() << " tokens.\n";
                script::Parser parser(std::move(tokens), m_config.scriptPath);
                auto program = parser.parse();
                std::cout << "[Engine] Parser: " << program.statements.size() << " statements.\n";
                m_scriptVM->loadProgram(std::move(program));
            } catch (const std::exception& e) {
                std::cerr << "[Engine] Script error: " << e.what() << "\n";
                if (!m_config.debugMode)
                    return false;
            }
        } else {
            std::cerr << "[Engine] Script not found: " << m_config.scriptPath << "\n";
            return false;
        }
    }

    m_running = true;
    m_state = EngineState::PLAYING;

    if (m_scriptVM && m_richText) {
        auto accentVal = m_scriptVM->getVariable("__config.ui_accent_color");
        if (auto* s = std::get_if<std::string>(&accentVal)) {
            if (!s->empty()) {
                auto col = text::RichTextParser::hexToColor(*s);
                m_richText->setAccentColor(col);
                std::cout << "[Engine] UI accent color: " << *s << "\n";
            }
        }
    }

    std::cout << "[Engine] Ready.\n";
    m_state = EngineState::SPLASH_SCREEN;
    m_splashTimer = 0.0f;
    return true;
}

void Engine::run() {
    const double fixedDt = m_timeManager->getFixedDeltaTime();
    double accumulator = 0.0;
    m_timeManager->reset();

    while (m_running) {
        m_timeManager->beginFrame();
        double frameTime = std::min(m_timeManager->getDeltaTime(), 0.25);
        accumulator += frameTime;

        processEvents();

        while (accumulator >= fixedDt) {
            update(fixedDt);
            accumulator -= fixedDt;
        }

        render(accumulator / fixedDt);
        m_timeManager->endFrame();
    }
}

void Engine::shutdown() {
    if (!m_sdlInitialized && !m_ttfInitialized && !m_window && !m_sdlRenderer)
        return;
    m_state = EngineState::SHUTTING_DOWN;
    m_scriptVM.reset();
    m_stateManager.reset();
    m_richText.reset();
    m_localization.reset();
    m_postEffects.reset();
    m_particles.reset();
    m_tweenManager.reset();
    m_renderer.reset();
    m_audioMixer.reset();
    m_assetManager.reset();
    m_timeManager.reset();
    m_settings.reset();
    if (m_fontTitle) {
        TTF_CloseFont(m_fontTitle);
        m_fontTitle = nullptr;
    }
    if (m_fontMenu) {
        TTF_CloseFont(m_fontMenu);
        m_fontMenu = nullptr;
    }
    if (m_fontSub) {
        TTF_CloseFont(m_fontSub);
        m_fontSub = nullptr;
    }
    if (m_fontSmall) {
        TTF_CloseFont(m_fontSmall);
        m_fontSmall = nullptr;
    }
    if (m_sdlRenderer) {
        SDL_DestroyRenderer(m_sdlRenderer);
        m_sdlRenderer = nullptr;
    }
    if (m_window) {
        SDL_DestroyWindow(m_window);
        m_window = nullptr;
    }
    if (m_ttfInitialized) {
        TTF_Quit();
        m_ttfInitialized = false;
    }
    if (m_sdlInitialized) {
        SDL_Quit();
        m_sdlInitialized = false;
    }
    m_running = false;
    std::cout << "[Engine] Shutdown complete.\n";
}

void Engine::hotReload() {
    if (!m_scriptVM || m_config.scriptPath.empty()) {
        m_statusMessage = "Hot Reload: no script";
        m_statusTimer = 2.0f;
        return;
    }

    std::filesystem::path sf(m_config.scriptPath);
    if (!std::filesystem::exists(sf)) {
        m_statusMessage = "Hot Reload: file not found";
        m_statusTimer = 2.0f;
        return;
    }

    size_t savedPC = m_scriptVM->getCurrentLine();
    std::cout << "[Engine] Hot Reload: reloading " << m_config.scriptPath << " (VM line " << savedPC
              << ")...\n";

    std::ifstream file(sf);
    std::stringstream buf;
    buf << file.rdbuf();
    file.close();

    try {
        script::Lexer lexer(buf.str(), m_config.scriptPath);
        auto tokens = lexer.tokenize();
        script::Parser parser(std::move(tokens), m_config.scriptPath);
        auto program = parser.parse();

        std::cout << "[Engine] Hot Reload: " << program.statements.size()
                  << " statements (previous PC=" << savedPC << ")\n";

        size_t progSize = program.statements.size();
        m_scriptVM->loadProgram(std::move(program));

        if (savedPC > 0 && savedPC < progSize) {
            m_scriptVM->setCurrentLine(savedPC);
        }

        m_scriptVM->forceState(script::VMState::WAITING_INPUT);

        m_statusMessage = "Hot Reload OK";
        m_statusTimer = 2.0f;
        std::cout << "[Engine] Hot Reload complete. Current line: " << m_scriptVM->getCurrentLine()
                  << "\n";

    } catch (const std::exception& e) {
        std::string errMsg = e.what();
        if (errMsg.size() > 60)
            errMsg = errMsg.substr(0, 60) + "...";
        m_statusMessage = "Hot Reload ERR: " + errMsg;
        m_statusTimer = 4.0f;
        std::cerr << "[Engine] Hot Reload failed: " << e.what() << "\n";
    }
}

void Engine::processEvents() {
    SDL_Event event;
    while (SDL_PollEvent(&event)) {
        SDL_ConvertEventToRenderCoordinates(m_sdlRenderer, &event);
        switch (event.type) {
        case SDL_EVENT_QUIT:
            m_running = false;
            break;
        case SDL_EVENT_KEY_DOWN:
            handleKeyDown(event.key);
            break;
        case SDL_EVENT_KEY_UP:
            if ((event.key.key == SDLK_LCTRL || event.key.key == SDLK_RCTRL) && m_skipMode) {
                m_skipMode = false;
                m_statusMessage = "Skip OFF";
                m_statusTimer = 1.0f;
            }
            break;
        case SDL_EVENT_MOUSE_BUTTON_DOWN:
            handleMouseDown(event.button);
            break;
        case SDL_EVENT_MOUSE_WHEEL:
            handleMouseWheel(event.wheel);
            break;
        case SDL_EVENT_MOUSE_MOTION:
            if (m_richText)
                m_richText->updateMenuHover(event.motion.x, event.motion.y);
            break;
        case SDL_EVENT_TEXT_INPUT:
            if (m_textInputActive && event.text.text) {
                m_textInputBuffer += event.text.text;
                std::cout << "[Engine] Text input buffer: \"" << m_textInputBuffer << "\"\n";
            }
            break;
        default:
            break;
        }
    }
}

void Engine::handleKeyDown(const SDL_KeyboardEvent& event) {
    if (m_textInputActive) {
        switch (event.key) {
        case SDLK_RETURN: {
            std::cout << "[Engine] TextInput submit: \"" << m_textInputBuffer << "\"\n";
            if (m_scriptVM)
                m_scriptVM->submitTextInput(m_textInputBuffer);
            m_textInputBuffer.clear();
            m_textInputActive = false;
            SDL_StopTextInput(m_window);
            break;
        }
        case SDLK_BACKSPACE:
            if (!m_textInputBuffer.empty()) {
                size_t pos = m_textInputBuffer.size();
                while (pos > 0 && (m_textInputBuffer[pos - 1] & 0xC0) == 0x80)
                    pos--;
                if (pos > 0)
                    pos--;
                m_textInputBuffer.erase(pos);
                std::cout << "[Engine] TextInput backspace: \"" << m_textInputBuffer << "\"\n";
            }
            break;
        case SDLK_ESCAPE:
            break;
        default:
            break;
        }
        return;
    }

    switch (event.key) {
    case SDLK_ESCAPE:
        if (m_showBacklog) {
            m_showBacklog = false;
            break;
        }
        if (m_confirmActive) {
            m_confirmActive = false;
            break;
        }
        if (m_state == EngineState::MAIN_MENU) {
            if (m_mainMenuScreen != MainMenuScreen::MAIN) {
                m_mainMenuScreen = MainMenuScreen::MAIN;
                m_mainMenuHover = -1;
                m_mainMenuFade = 0.6f;
            }
            break;
        }
        if (m_state == EngineState::GAME_MENU) {
            if (m_gameMenuTab == GameMenuTab::SETTINGS) {
                m_gameMenuTab = GameMenuTab::MAIN;
            } else {
                requestStateChange(EngineState::PLAYING);
            }
        } else if (m_state == EngineState::PLAYING) {
            m_gameMenuTab = GameMenuTab::MAIN;
            requestStateChange(EngineState::GAME_MENU);
        }
        break;
    case SDLK_SPACE:
    case SDLK_RETURN:
        if (m_showBacklog) {
            m_showBacklog = false;
            break;
        }
        if (m_state == EngineState::PLAYING) {
            if (m_richText && m_richText->isMenuActive())
                break;
            m_autoMode = false;
            m_skipMode = false;
            advanceDialogue();
        }
        break;
    case SDLK_F11: {
        Uint64 f = SDL_GetWindowFlags(m_window);
        SDL_SetWindowFullscreen(m_window, !(f & SDL_WINDOW_FULLSCREEN));
    } break;
    case SDLK_F5:
        if (m_saveLocked) {
            m_statusMessage = "Saving is locked";
            m_statusTimer = 2.0f;
            std::cout << "[Engine] F5: saving is locked by meta lock_save\n";
        } else if (m_stateManager) {
            m_stateManager->quickSave();
            m_statusMessage = "Quick Save";
            m_statusTimer = 2.0f;
        }
        break;
    case SDLK_F7:
        if (m_stateManager) {
            m_stateManager->quickLoad();
            restoreFromSnapshot();
            m_statusMessage = "Quick Load";
            m_statusTimer = 2.0f;
        }
        break;
    case SDLK_A:
        if (m_state == EngineState::PLAYING && !isTextInputMode()) {
            m_autoMode = !m_autoMode;
            m_skipMode = false;
            m_statusMessage = m_autoMode ? "Auto ON" : "Auto OFF";
            m_statusTimer = 1.5f;
        }
        break;
    case SDLK_H:
        if (m_state == EngineState::PLAYING && !isTextInputMode())
            m_hideUI = !m_hideUI;
        break;
    case SDLK_S:
        if (m_state == EngineState::PLAYING && !isTextInputMode()) {
            m_gameMenuTab = GameMenuTab::SAVE;
            requestStateChange(EngineState::GAME_MENU);
        }
        break;
    case SDLK_L:
        if (m_state == EngineState::PLAYING && !isTextInputMode()) {
            m_gameMenuTab = GameMenuTab::LOAD;
            requestStateChange(EngineState::GAME_MENU);
        }
        break;
    case SDLK_LCTRL:
    case SDLK_RCTRL:
        if (m_state == EngineState::PLAYING && !m_skipMode) {
            m_skipMode = true;
            m_autoMode = false;
            m_statusMessage = "Skip";
            m_statusTimer = 1.5f;
        }
        break;
    case SDLK_PAGEUP:
        if (m_state == EngineState::PLAYING) {
            m_showBacklog = !m_showBacklog;
            m_backlogScroll = 0.0f;
        }
        break;
    case SDLK_R:
        if ((event.mod & SDL_KMOD_SHIFT) && m_state == EngineState::PLAYING) {
            hotReload();
        }
        break;
    case SDLK_1:
    case SDLK_2:
    case SDLK_3:
    case SDLK_4:
    case SDLK_5:
    case SDLK_6:
    case SDLK_7:
    case SDLK_8:
    case SDLK_9: {
        if (m_state == EngineState::PLAYING && m_richText && m_richText->isMenuActive() &&
            m_scriptVM) {
            int idx = static_cast<int>(event.key - SDLK_1);
            m_richText->hideMenu();
            if (m_scriptVM->getState() == script::VMState::WAITING_CHOICE &&
                !m_scriptVM->getTextInputOptions().empty()) {
                const auto& opts = m_scriptVM->getTextInputOptions();
                if (idx >= 0 && idx < static_cast<int>(opts.size())) {
                    m_scriptVM->submitTextInput(opts[idx]);
                    std::cout << "[Engine] Input choice selected: [" << idx << "] " << opts[idx]
                              << "\n";
                }
            } else {
                m_scriptVM->selectChoice(idx);
            }
        }
    } break;
    default:
        break;
    }
}

void Engine::handleMouseDown(const SDL_MouseButtonEvent& event) {
    if (event.button == SDL_BUTTON_LEFT && m_state == EngineState::MAIN_MENU) {
        float w = static_cast<float>(DESIGN_W);
        float h = static_cast<float>(DESIGN_H);
        float wmx, wmy;
        SDL_GetMouseState(&wmx, &wmy);
        float mx, my;
        SDL_RenderCoordinatesFromWindow(m_sdlRenderer, wmx, wmy, &mx, &my);

        if (m_confirmActive) {
            float dw = 400, dh = 160;
            float dx = w * 0.5f - dw * 0.5f, dy = h * 0.5f - dh * 0.5f;
            float btnW = 120, btnH = 36;
            float btnY = dy + dh - 55;
            if (mx >= dx + 30 && mx <= dx + 30 + btnW && my >= btnY && my <= btnY + btnH) {
                applySettings();
                m_confirmActive = false;
                m_mainMenuScreen = MainMenuScreen::MAIN;
            } else if (mx >= dx + dw / 2 - btnW / 2 && mx <= dx + dw / 2 + btnW / 2 && my >= btnY &&
                       my <= btnY + btnH) {
                m_confirmActive = false;
                m_mainMenuScreen = MainMenuScreen::MAIN;
            } else if (mx >= dx + dw - 30 - btnW && mx <= dx + dw - 30 && my >= btnY &&
                       my <= btnY + btnH) {
                m_confirmActive = false;
            }
            return;
        }

        if (m_mainMenuScreen == MainMenuScreen::SAVES) {
            float slotX = 40, slotW = w * 0.55f, slotH = 52;
            for (int i = 0; i < 9; i++) {
                float sy = 80 + i * (slotH + 8);
                if (mx >= slotX && mx <= slotX + slotW && my >= sy && my <= sy + slotH) {
                    m_selectedSaveSlot = i + 1;
                    return;
                }
            }
            float btnRX = w - 260, btnRY = h - 110, btnRW = 220, btnRH = 42;
            if (m_selectedSaveSlot > 0 && mx >= btnRX && mx <= btnRX + btnRW && my >= btnRY &&
                my <= btnRY + btnRH) {
                if (m_stateManager && m_stateManager->loadFromSlot(m_selectedSaveSlot)) {
                    restoreFromSnapshot();
                    m_state = EngineState::PLAYING;
                    m_mainMenuScreen = MainMenuScreen::MAIN;
                    m_selectedSaveSlot = -1;
                }
                return;
            }
            float backY = h - 58;
            if (mx >= btnRX && mx <= btnRX + btnRW && my >= backY && my <= backY + btnRH) {
                m_mainMenuScreen = MainMenuScreen::MAIN;
                m_selectedSaveSlot = -1;
                m_mainMenuHover = -1;
                m_mainMenuFade = 0.6f;
                return;
            }
            return;
        }

        if (m_mainMenuScreen == MainMenuScreen::SETTINGS) {
            handleGameMenuClick(mx, my);
            return;
        }

        if (m_mainMenuScreen == MainMenuScreen::CREDITS) {
            float btnRX = w - 260, btnRW = 220, btnRH = 42;
            float backY = h - 58;
            if (mx >= btnRX && mx <= btnRX + btnRW && my >= backY && my <= backY + btnRH) {
                m_mainMenuScreen = MainMenuScreen::MAIN;
                m_mainMenuHover = -1;
                m_mainMenuFade = 0.6f;
            }
            return;
        }

        if (m_mainMenuHover >= 0) {
            bool hasSave = false;
            if (m_stateManager) {
                auto slots = m_stateManager->getSaveSlots();
                for (auto& s : slots)
                    if (s.occupied) {
                        hasSave = true;
                        break;
                    }
            }
            int action = m_mainMenuHover;
            if (!hasSave)
                action += 1;

            switch (action) {
            case 0:
                if (m_stateManager && m_stateManager->quickLoad()) {
                    restoreFromSnapshot();
                    m_state = EngineState::PLAYING;
                }
                break;
            case 1:
                m_state = EngineState::PLAYING;
                if (m_scriptVM)
                    m_scriptVM->advance();
                break;
            case 2:
                m_mainMenuScreen = MainMenuScreen::SAVES;
                m_selectedSaveSlot = -1;
                m_mainMenuHover = -1;
                break;
            case 3:
                m_mainMenuScreen = MainMenuScreen::SETTINGS;
                m_gameMenuTab = GameMenuTab::SETTINGS;
                m_pendingDisplayMode = m_displayMode;
                m_pendingLocale = m_locale;
                m_pendingSkipEffects = m_skipEffects;
                {
                    int rw[] = {1280, 1366, 1600, 1920};
                    int rh[] = {720, 768, 900, 1080};
                    m_pendingResIndex = 0;
                    for (int i = 0; i < 4; i++)
                        if (m_config.windowWidth == rw[i] && m_config.windowHeight == rh[i])
                            m_pendingResIndex = i;
                }
                m_settingsChanged = false;
                m_mainMenuHover = -1;
                break;
            case 4:
                m_mainMenuScreen = MainMenuScreen::CREDITS;
                m_mainMenuHover = -1;
                break;
            case 5:
                m_running = false;
                break;
            }
        }
        return;
    }

    if (event.button == SDL_BUTTON_LEFT && m_state == EngineState::GAME_MENU) {
        handleGameMenuClick(event.x, event.y);
        return;
    }
    if (event.button == SDL_BUTTON_LEFT && m_state == EngineState::PLAYING) {
        if (m_showBacklog) {
            m_showBacklog = false;
            return;
        }
        if (m_richText && m_richText->isMenuActive()) {
            int choice = m_richText->handleMenuClick(event.x, event.y);
            if (choice >= 0 && m_scriptVM) {
                m_richText->hideMenu();
                if (m_scriptVM->getState() == script::VMState::WAITING_CHOICE &&
                    !m_scriptVM->getTextInputOptions().empty()) {
                    const auto& opts = m_scriptVM->getTextInputOptions();
                    if (choice >= 0 && choice < static_cast<int>(opts.size())) {
                        std::string choiceText = opts[choice];
                        m_scriptVM->submitTextInput(choiceText);
                        std::cout << "[Engine] Input choice click: [" << choice << "] "
                                  << choiceText << "\n";
                    }
                } else {
                    m_scriptVM->selectChoice(choice);
                }
                return;
            }
            return;
        }
        if (m_richText && m_richText->isActive()) {
            float w = static_cast<float>(DESIGN_W);
            float h = static_cast<float>(DESIGN_H);
            float barH = 28.0f, barY = h - barH;
            if (event.y >= barY) {
                TTF_Font* qf = m_richText->getFont();
                if (qf) {
                    const char* qlabels[] = {
                        "Menu", "Save", "Load", "Auto", "Skip", "History", "Prefs", "Hide",
                    };
                    const int N = 8;
                    float spacing = 28.0f;
                    float totalW = 0;
                    float iw[8];
                    for (int i = 0; i < N; i++) {
                        SDL_Surface* tmp = TTF_RenderText_Blended(
                            qf, qlabels[i], strlen(qlabels[i]), {255, 255, 255, 255});
                        iw[i] = tmp ? static_cast<float>(tmp->w) : 50.0f;
                        if (tmp)
                            SDL_DestroySurface(tmp);
                        totalW += iw[i];
                    }
                    totalW += (N - 1) * spacing;
                    float cx = (w - totalW) * 0.5f;
                    for (int i = 0; i < N; i++) {
                        if (event.x >= cx && event.x <= cx + iw[i]) {
                            switch (i) {
                            case 0:
                                m_gameMenuTab = GameMenuTab::MAIN;
                                requestStateChange(EngineState::GAME_MENU);
                                break;
                            case 1:
                                if (m_stateManager)
                                    m_stateManager->quickSave();
                                m_statusMessage = "Saved";
                                m_statusTimer = 2.0f;
                                break;
                            case 2:
                                if (m_stateManager) {
                                    m_stateManager->quickLoad();
                                    restoreFromSnapshot();
                                }
                                m_statusMessage = "Loaded";
                                m_statusTimer = 2.0f;
                                break;
                            case 3:
                                m_autoMode = !m_autoMode;
                                m_skipMode = false;
                                m_statusMessage = m_autoMode ? "Auto: ON" : "Auto: OFF";
                                m_statusTimer = 1.5f;
                                break;
                            case 4:
                                m_skipMode = !m_skipMode;
                                m_autoMode = false;
                                m_statusMessage = m_skipMode ? "Skip: ON" : "Skip: OFF";
                                m_statusTimer = 1.5f;
                                break;
                            case 5:
                                m_showBacklog = true;
                                m_backlogScroll = 0.0f;
                                break;
                            case 6:
                                m_gameMenuTab = GameMenuTab::SETTINGS;
                                requestStateChange(EngineState::GAME_MENU);
                                break;
                            case 7:
                                m_hideUI = !m_hideUI;
                                break;
                            }
                            return;
                        }
                        cx += iw[i] + spacing;
                    }
                }
                return;
            }
        }
        m_autoMode = false;
        m_skipMode = false;
        advanceDialogue();
    } else if (event.button == SDL_BUTTON_RIGHT) {
        if (m_state == EngineState::PLAYING) {
            m_gameMenuTab = GameMenuTab::MAIN;
            requestStateChange(EngineState::GAME_MENU);
        } else if (m_state == EngineState::GAME_MENU)
            requestStateChange(EngineState::PLAYING);
    }
}

void Engine::handleMouseWheel(const SDL_MouseWheelEvent& event) {
    if (m_showBacklog) {
        m_backlogScroll -= event.y * 60.0f;
        if (m_backlogScroll < 0.0f)
            m_backlogScroll = 0.0f;
        return;
    }
    if (event.y > 0 && m_state == EngineState::PLAYING && !m_dialogueHistory.empty()) {
        m_showBacklog = true;
        m_backlogScroll = 0.0f;
    }
}

void Engine::advanceDialogue() {
    if (m_richText && m_richText->isActive() && !m_richText->isComplete()) {
        m_richText->skipTypewriter();
        return;
    }

    if (m_scriptVM && m_scriptVM->getState() == script::VMState::FINISHED) {
        SDL_SetRenderDrawColor(m_sdlRenderer, 10, 10, 15, 255);
        SDL_RenderClear(m_sdlRenderer);
        if (m_richText) {
            m_richText->beginDialogue("", "", "");
            m_richText->beginDialogue("Loading...", "#78909C", "");
            m_richText->skipTypewriter();
            m_richText->render(m_sdlRenderer);
        }
        SDL_RenderPresent(m_sdlRenderer);

        std::cout << "[Engine] Script finished. Restarting...\n";
        if (m_richText)
            m_richText->hideMenu();
        if (m_renderer)
            m_renderer->clearAllSprites(std::nullopt);

        try {
            std::ifstream file(m_config.scriptPath);
            if (file.is_open()) {
                std::stringstream buf;
                buf << file.rdbuf();
                script::Lexer lexer(buf.str(), m_config.scriptPath);
                auto tokens = lexer.tokenize();
                script::Parser parser(std::move(tokens), m_config.scriptPath);
                auto program = parser.parse();
                m_scriptVM->loadProgram(std::move(program));
            }
        } catch (const std::exception& e) {
            std::cerr << "[Engine] Restart failed: " << e.what() << "\n";
        }
        return;
    }

    if (m_scriptVM)
        m_scriptVM->advance();
}

void Engine::restoreFromSnapshot() {
    if (!m_stateManager || !m_scriptVM)
        return;
    const auto* snap = m_stateManager->getCurrentSnapshot();
    if (!snap) {
        std::cout << "[Engine] No snapshot to restore.\n";
        return;
    }

    size_t targetLine = snap->scriptLine;
    std::string savedSpeaker = snap->currentSpeaker;
    std::string savedText = snap->currentText;
    auto savedVariables = snap->variables;
    std::string savedBg = snap->backgroundPath;
    auto savedSprites = snap->sprites;
    std::cout << "[Engine] Restoring to line " << targetLine << "\n";

    if (m_renderer)
        m_renderer->clearAllSprites(std::nullopt);
    if (m_audioMixer)
        m_audioMixer->stopAll();
    if (m_particles)
        m_particles->stopAll();
    if (m_richText) {
        m_richText->beginDialogue("", "", "");
        m_richText->hideMenu();
    }

    try {
        std::ifstream file(m_config.scriptPath);
        if (!file.is_open()) {
            std::cerr << "[Engine] Restore: script not found\n";
            return;
        }
        std::stringstream buf;
        buf << file.rdbuf();
        script::Lexer lexer(buf.str(), m_config.scriptPath);
        auto tokens = lexer.tokenize();
        script::Parser parser(std::move(tokens), m_config.scriptPath);
        auto program = parser.parse();
        m_scriptVM->loadProgram(std::move(program));
    } catch (const std::exception& e) {
        std::cerr << "[Engine] Restore failed: " << e.what() << "\n";
        return;
    }

    m_replaying = true;
    int safetyLimit = 5000;
    while (m_scriptVM->getCurrentLine() <= targetLine &&
           m_scriptVM->getState() != script::VMState::FINISHED && safetyLimit-- > 0) {

        auto st = m_scriptVM->getState();

        if (st == script::VMState::RUNNING) {
            m_scriptVM->update(0.0);
        } else if (st == script::VMState::WAITING_TIMED) {
            m_scriptVM->update(999.0);
        } else if (st == script::VMState::WAITING_INPUT) {
            if (m_richText)
                m_richText->skipTypewriter();
            m_scriptVM->advance();
        } else if (st == script::VMState::WAITING_CHOICE) {
            m_scriptVM->selectChoice(0);
        } else if (st == script::VMState::WAITING_TEXT_INPUT) {
            auto& opts = m_scriptVM->getTextInputOptions();
            if (!opts.empty()) {
                m_scriptVM->submitTextInput(opts[0]);
            } else {
                m_scriptVM->submitTextInput("");
            }
        } else {
            break;
        }
    }
    m_replaying = false;

    for (const auto& [key, val] : savedVariables) {
        m_scriptVM->setVariable(key, val);
    }
    std::cout << "[Engine] Restored " << savedVariables.size() << " variables.\n";

    if (!savedBg.empty() && m_renderer) {
        m_renderer->setBackground(savedBg, std::nullopt);
        std::cout << "[Engine] Restored background: " << savedBg << "\n";
    }

    if (m_renderer) {
        for (const auto& sprite : savedSprites) {
            script::PositionSpec position;
            position.x = sprite.x;
            position.y = sprite.y;
            m_renderer->showSprite(sprite.id, sprite.imagePath, position, std::nullopt,
                                   sprite.flipped, sprite.zOrder, sprite.scaleX, sprite.rotation);
        }
    }

    if (m_richText) {
        auto accentVal = m_scriptVM->getVariable("__config.ui_accent_color");
        if (auto* s = std::get_if<std::string>(&accentVal)) {
            if (!s->empty()) {
                auto col = text::RichTextParser::hexToColor(*s);
                m_richText->setAccentColor(col);
            }
        }
    }

    if (m_richText && !savedText.empty()) {
        std::string speakerColor = "#B0BEC5";
        if (!savedSpeaker.empty() && m_scriptVM) {
            auto* ch = m_scriptVM->getCharacterByDisplayName(savedSpeaker);
            if (ch)
                speakerColor = ch->color;
            else
                speakerColor = "#FFFFFF";
        }
        m_richText->beginDialogue(savedSpeaker, speakerColor, savedText);
        m_richText->skipTypewriter();
    }

    if (m_tweenManager) {
        for (int i = 0; i < 60; i++)
            m_tweenManager->update(0.1);
    }

    m_state = EngineState::PLAYING;
    std::cout << "[Engine] Restored to line " << targetLine << " (VM at "
              << m_scriptVM->getCurrentLine() << ")\n";
}

void Engine::requestStateChange(EngineState s) {
    m_state = s;
}

static void drawText(SDL_Renderer* r, TTF_Font* font, const char* text, float cx, float cy,
                     SDL_Color color, bool center = true) {
    if (!font || !text || !text[0])
        return;
    size_t len = strlen(text);
    SDL_Surface* surf = TTF_RenderText_Blended(font, text, len, color);
    if (!surf)
        return;
    SDL_Texture* tex = SDL_CreateTextureFromSurface(r, surf);
    float tw = static_cast<float>(surf->w);
    float th = static_cast<float>(surf->h);
    SDL_DestroySurface(surf);
    if (!tex)
        return;
    float dx = center ? cx - tw * 0.5f : cx;
    float dy = center ? cy - th * 0.5f : cy;
    SDL_FRect dst = {dx, dy, tw, th};
    SDL_RenderTexture(r, tex, nullptr, &dst);
    SDL_DestroyTexture(tex);
}

void Engine::update(double dt) {
    if (m_tweenManager)
        m_tweenManager->update(dt);
    if (m_renderer)
        m_renderer->update(dt);
    if (m_audioMixer)
        m_audioMixer->update(dt);
    if (m_richText)
        m_richText->update(dt);
    if (m_particles)
        m_particles->update(dt, static_cast<float>(DESIGN_W), static_cast<float>(DESIGN_H));
    if (m_statusTimer > 0.0f)
        m_statusTimer -= static_cast<float>(dt);

    switch (m_state) {
    case EngineState::SPLASH_SCREEN: {
        m_splashTimer += static_cast<float>(dt);
        m_splashProgress = std::min(1.0f, m_splashTimer / (m_splashDuration * 0.8f));
        if (m_splashTimer > m_splashDuration - 0.5f) {
            m_splashFade = (m_splashTimer - (m_splashDuration - 0.5f)) / 0.5f;
            m_splashFade = std::min(1.0f, m_splashFade);
        }
        if (m_splashTimer >= m_splashDuration) {
            m_state = EngineState::MAIN_MENU;
            m_mainMenuFade = 0.0f;
            m_mainMenuTitleY = -30.0f;
            m_mainMenuHover = -1;
            for (auto& a : m_mainMenuBtnAnim)
                a = 0.0f;
        }
        break;
    }
    case EngineState::PLAYING:
        updatePlaying(dt);
        break;
    case EngineState::MAIN_MENU:
        updateMainMenu(dt);
        break;
    case EngineState::PAUSED:
        updatePaused(dt);
        break;
    case EngineState::GAME_MENU:
        break;
    case EngineState::IN_MENU:
        updateInMenu(dt);
        break;
    default:
        break;
    }
}

void Engine::updatePlaying(double dt) {
    if (m_scriptVM) {
        m_scriptVM->update(dt);

        if (m_scriptVM->getState() == script::VMState::WAITING_TEXT_INPUT && !m_textInputActive) {
            m_textInputActive = true;
            m_textInputBuffer.clear();
            m_cursorBlink = 0.0f;
            m_inputSlideAnim = 0.0f;
            SDL_StartTextInput(m_window);
            std::cout << "[Engine] Text input activated: \"" << m_scriptVM->getTextInputPrompt()
                      << "\"\n";
        }
    }

    if (m_textInputActive) {
        m_cursorBlink += static_cast<float>(dt);
        if (m_cursorBlink > 1.0f)
            m_cursorBlink -= 1.0f;
        if (m_inputSlideAnim < 1.0f) {
            m_inputSlideAnim += static_cast<float>(dt) * 3.0f;
            if (m_inputSlideAnim > 1.0f)
                m_inputSlideAnim = 1.0f;
        }
    } else {
        m_inputSlideAnim = 0.0f;
    }

    if (m_glitchTextTimer > 0.0)
        m_glitchTextTimer -= dt;
    if (m_glitchUITimer > 0.0)
        m_glitchUITimer -= dt;
    if (m_postEffects)
        m_postEffects->update(dt);
    if (m_windowShakeTimer > 0.0) {
        m_windowShakeTimer -= dt;
        if (m_windowShakeTimer > 0.0) {
            int shakeX = (rand() % 20) - 10;
            int shakeY = (rand() % 20) - 10;
            SDL_SetWindowPosition(m_window, m_windowOrigX + shakeX, m_windowOrigY + shakeY);
        } else {
            SDL_SetWindowPosition(m_window, m_windowOrigX, m_windowOrigY);
        }
    }

    if (m_autoMode && m_richText && m_richText->isComplete() && !m_richText->isMenuActive()) {
        m_autoTimer += static_cast<float>(dt);
        if (m_autoTimer > m_autoDelay) {
            m_autoTimer = 0;
            advanceDialogue();
        }
    } else {
        m_autoTimer = 0;
    }

    if (m_skipMode && m_richText && !m_richText->isMenuActive()) {
        if (m_scriptVM) {
            auto vmState = m_scriptVM->getState();
            if (vmState == script::VMState::WAITING_TEXT_INPUT ||
                vmState == script::VMState::WAITING_CHOICE) {
                m_skipMode = false;
            } else {
                if (m_skipEffects && vmState == script::VMState::WAITING_TIMED) {
                    m_scriptVM->update(999.0);
                }
                m_skipTimer += static_cast<float>(dt);
                float skipInterval = 1.0f / m_skipSpeed;
                if (m_skipTimer >= skipInterval) {
                    m_skipTimer -= skipInterval;
                    if (!m_richText->isComplete())
                        m_richText->skipTypewriter();
                    else
                        advanceDialogue();
                }
            }
        }
    } else {
        m_skipTimer = 0.0f;
    }
}
void Engine::updateMainMenu(double dt) {
    float fdt = static_cast<float>(dt);

    if (m_mainMenuFade < 1.0f) {
        m_mainMenuFade += fdt * 1.2f;
        if (m_mainMenuFade > 1.0f)
            m_mainMenuFade = 1.0f;
    }

    if (m_mainMenuTitleY < 0.0f) {
        m_mainMenuTitleY += fdt * 40.0f;
        if (m_mainMenuTitleY > 0.0f)
            m_mainMenuTitleY = 0.0f;
    }

    for (int i = 0; i < 8; i++) {
        float target = (i == m_mainMenuHover) ? 1.0f : 0.0f;
        float speed = 5.0f;
        if (m_mainMenuBtnAnim[i] < target) {
            m_mainMenuBtnAnim[i] += fdt * speed;
            if (m_mainMenuBtnAnim[i] > target)
                m_mainMenuBtnAnim[i] = target;
        } else if (m_mainMenuBtnAnim[i] > target) {
            m_mainMenuBtnAnim[i] -= fdt * speed;
            if (m_mainMenuBtnAnim[i] < target)
                m_mainMenuBtnAnim[i] = target;
        }
    }

    if (m_mainMenuScreen != MainMenuScreen::MAIN) {
        m_mainMenuHover = -1;
        return;
    }

    float wmx, wmy;
    SDL_GetMouseState(&wmx, &wmy);
    float mx, my;
    SDL_RenderCoordinatesFromWindow(m_sdlRenderer, wmx, wmy, &mx, &my);
    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);

    float btnX = w * 0.06f;
    float btnW = 320.0f;
    float btnH = 50.0f;
    float btnStartY = h * 0.38f;
    float btnSpacing = 56.0f;

    bool hasSave = false;
    if (m_stateManager) {
        auto slots = m_stateManager->getSaveSlots();
        for (auto& s : slots)
            if (s.occupied) {
                hasSave = true;
                break;
            }
    }

    int btnCount = hasSave ? 6 : 5;
    m_mainMenuHover = -1;

    for (int i = 0; i < btnCount; i++) {
        float by = btnStartY + i * btnSpacing;
        if (mx >= btnX && mx <= btnX + btnW && my >= by && my <= by + btnH) {
            m_mainMenuHover = i;
            break;
        }
    }
}
void Engine::updatePaused(double dt) {
    (void)dt;
}
void Engine::updateInMenu(double dt) {
    (void)dt;
}

void Engine::render(double alpha) {
    SDL_SetRenderDrawColor(m_sdlRenderer, 10, 10, 15, 255);
    SDL_RenderClear(m_sdlRenderer);

    if (m_postEffects && m_postEffects->hasActiveEffects()) {
        m_postEffects->beginCapture(m_sdlRenderer);
    }

    switch (m_state) {
    case EngineState::SPLASH_SCREEN: {
        SDL_SetRenderDrawColor(m_sdlRenderer, 10, 10, 14, 255);
        SDL_RenderClear(m_sdlRenderer);

        float w = static_cast<float>(DESIGN_W);
        float h = static_cast<float>(DESIGN_H);

        float fadeIn = std::min(m_splashTimer / 0.8f, 1.0f);
        float fadeOut = (m_splashTimer > m_splashDuration - 0.6f)
                            ? 1.0f - (m_splashTimer - (m_splashDuration - 0.6f)) / 0.6f
                            : 1.0f;
        float a = fadeIn * std::max(fadeOut, 0.0f);
        uint8_t ta = static_cast<uint8_t>(a * 255);

        TTF_Font* fT = m_fontTitle ? m_fontTitle : (m_richText ? m_richText->getFont() : nullptr);
        if (fT) {
            drawText(m_sdlRenderer, fT, "VELORA ENGINE", w * 0.5f, h * 0.45f, {255, 255, 255, ta},
                     true);
            SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
            SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, static_cast<uint8_t>(ta * 0.4f));
            SDL_FRect line = {w * 0.35f, h * 0.52f, w * 0.3f, 1.5f};
            SDL_RenderFillRect(m_sdlRenderer, &line);
        }
        break;
    }
    case EngineState::PLAYING:
        renderPlaying(alpha);
        break;
    case EngineState::MAIN_MENU:
        renderMainMenu(alpha);
        break;
    case EngineState::PAUSED:
        renderPlaying(alpha);
        renderPaused(alpha);
        break;
    case EngineState::GAME_MENU:
        renderPlaying(alpha);
        renderGameMenu(alpha);
        break;
    case EngineState::IN_MENU:
        renderPlaying(alpha);
        renderInMenu(alpha);
        break;
    default:
        break;
    }

    if (m_statusTimer > 0.0f) {
        float a = std::min(m_statusTimer, 1.0f);
        SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
        SDL_SetRenderDrawColor(m_sdlRenderer, 20, 20, 30, static_cast<uint8_t>(180 * a));
        SDL_FRect badge = {static_cast<float>(DESIGN_W) - 220, 20, 200, 40};
        SDL_RenderFillRect(m_sdlRenderer, &badge);
        SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, static_cast<uint8_t>(200 * a));
        SDL_RenderRect(m_sdlRenderer, &badge);

        TTF_Font* f = m_richText ? m_richText->getFont() : nullptr;
        if (f && !m_statusMessage.empty()) {
            SDL_Color tc = {220, 200, 255, static_cast<uint8_t>(255 * a)};
            drawText(m_sdlRenderer, f, m_statusMessage.c_str(), badge.x + badge.w * 0.5f,
                     badge.y + badge.h * 0.5f, tc);
        }
    }

    if (m_postEffects && m_postEffects->hasActiveEffects()) {
        m_postEffects->endCapture(m_sdlRenderer, m_settings.get());
    }

    SDL_RenderPresent(m_sdlRenderer);
}

void Engine::renderPlaying(double alpha) {
    if (m_renderer) {
        m_renderer->renderBackground(alpha);
        m_renderer->renderSprites(alpha);
        m_renderer->renderEffects(alpha);
    }
    if (m_particles)
        m_particles->render(m_sdlRenderer);

    if (m_scriptVM && !m_scriptVM->getActiveScreens().empty()) {
        int logW = 0, logH = 0;
        SDL_GetRenderLogicalPresentation(m_sdlRenderer, &logW, &logH, nullptr);
        if (logW <= 0)
            logW = m_config.windowWidth;
        if (logH <= 0)
            logH = m_config.windowHeight;
        float sw = static_cast<float>(logW);
        float sh = static_cast<float>(logH);

        for (const auto& [name, screen] : m_scriptVM->getActiveScreens()) {
            renderScreenElements(screen.elements, sw, sh);
        }
    }

    if (!m_hideUI) {
        if (m_richText) {
            m_richText->render(m_sdlRenderer);
            m_richText->renderMenu(m_sdlRenderer);
        }
        if (m_quickbarVisible)
            renderQuickBar(alpha);
    }
    if (m_showBacklog)
        renderBacklog(alpha);

    if (m_textInputActive && m_richText) {
        TTF_Font* font = m_richText->getFont();
        if (font) {
            float w = static_cast<float>(DESIGN_W);
            float h = static_cast<float>(DESIGN_H);

            float t = m_inputSlideAnim;
            float eased = 1.0f - (1.0f - t) * (1.0f - t);

            uint8_t dimAlpha = static_cast<uint8_t>(160.0f * eased);
            SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
            SDL_SetRenderDrawColor(m_sdlRenderer, 8, 6, 4, dimAlpha);
            SDL_FRect overlay = {0, 0, w, h};
            SDL_RenderFillRect(m_sdlRenderer, &overlay);

            float cardW = 520.0f;
            float cardX = (w - cardW) * 0.5f;
            float promptMaxW = cardW - 40.0f;

            std::string prompt = m_scriptVM ? m_scriptVM->getTextInputPrompt() : "";
            float promptH = 24.0f;
            if (!prompt.empty()) {
                SDL_Color tmp = {0, 0, 0, 255};
                SDL_Surface* measure = TTF_RenderText_Blended_Wrapped(
                    font, prompt.c_str(), prompt.size(), tmp, static_cast<int>(promptMaxW));
                if (measure) {
                    promptH = static_cast<float>(measure->h);
                    SDL_DestroySurface(measure);
                }
            }

            float cardH = 50.0f + promptH + 15.0f + 50.0f + 40.0f + 20.0f;
            if (cardH < 220.0f)
                cardH = 220.0f;

            float cardTargetY = (h - cardH) * 0.5f;
            float cardY = h + (cardTargetY - h) * eased;

            SDL_SetRenderDrawColor(m_sdlRenderer, 0, 0, 0, static_cast<uint8_t>(80 * eased));
            SDL_FRect shadow = {cardX + 6, cardY + 6, cardW, cardH};
            SDL_RenderFillRect(m_sdlRenderer, &shadow);

            SDL_SetRenderDrawColor(m_sdlRenderer, 235, 220, 190, static_cast<uint8_t>(245 * eased));
            SDL_FRect card = {cardX, cardY, cardW, cardH};
            SDL_RenderFillRect(m_sdlRenderer, &card);

            SDL_SetRenderDrawColor(m_sdlRenderer, 140, 110, 70, static_cast<uint8_t>(200 * eased));
            SDL_RenderRect(m_sdlRenderer, &card);
            SDL_FRect innerBorder = {cardX + 4, cardY + 4, cardW - 8, cardH - 8};
            SDL_SetRenderDrawColor(m_sdlRenderer, 160, 130, 85, static_cast<uint8_t>(120 * eased));
            SDL_RenderRect(m_sdlRenderer, &innerBorder);

            SDL_Rect cardClip = {static_cast<int>(cardX), static_cast<int>(cardY),
                                 static_cast<int>(cardW), static_cast<int>(cardH)};
            SDL_SetRenderClipRect(m_sdlRenderer, &cardClip);

            const char* title = "TEXT INPUT";
            SDL_Color titleCol = {90, 65, 35, static_cast<uint8_t>(220 * eased)};
            SDL_Surface* titleSurf = TTF_RenderText_Blended(font, title, strlen(title), titleCol);
            if (titleSurf) {
                SDL_Texture* titleTex = SDL_CreateTextureFromSurface(m_sdlRenderer, titleSurf);
                float titleX = cardX + (cardW - titleSurf->w) * 0.5f;
                SDL_FRect titleRect = {titleX, cardY + 14, static_cast<float>(titleSurf->w),
                                       static_cast<float>(titleSurf->h)};
                SDL_RenderTexture(m_sdlRenderer, titleTex, nullptr, &titleRect);
                SDL_DestroyTexture(titleTex);
                SDL_DestroySurface(titleSurf);
            }

            float sepY = cardY + 44;
            SDL_SetRenderDrawColor(m_sdlRenderer, 140, 110, 70, static_cast<uint8_t>(100 * eased));
            SDL_RenderLine(m_sdlRenderer, cardX + 20, sepY, cardX + cardW - 20, sepY);

            float promptY = cardY + 52;
            SDL_Color promptCol = {70, 55, 35, static_cast<uint8_t>(200 * eased)};
            if (!prompt.empty()) {
                SDL_Surface* ps = TTF_RenderText_Blended_Wrapped(
                    font, prompt.c_str(), prompt.size(), promptCol, static_cast<int>(promptMaxW));
                if (ps) {
                    SDL_Texture* pt = SDL_CreateTextureFromSurface(m_sdlRenderer, ps);
                    float px = cardX + (cardW - ps->w) * 0.5f;
                    SDL_FRect pr = {px, promptY, static_cast<float>(ps->w),
                                    static_cast<float>(ps->h)};
                    SDL_RenderTexture(m_sdlRenderer, pt, nullptr, &pr);
                    SDL_DestroyTexture(pt);
                    SDL_DestroySurface(ps);
                }
            }

            float fieldX = cardX + 40;
            float fieldY2 = promptY + promptH + 12;
            float fieldW2 = cardW - 80, fieldH2 = 36;

            SDL_SetRenderDrawColor(m_sdlRenderer, 220, 205, 175, static_cast<uint8_t>(200 * eased));
            SDL_FRect fieldBg = {fieldX, fieldY2, fieldW2, fieldH2};
            SDL_RenderFillRect(m_sdlRenderer, &fieldBg);

            SDL_SetRenderDrawColor(m_sdlRenderer, 120, 95, 60, static_cast<uint8_t>(180 * eased));
            float lineYf = fieldY2 + fieldH2 - 2;
            for (float dx = 0; dx < fieldW2; dx += 8.0f) {
                SDL_RenderLine(m_sdlRenderer, fieldX + dx, lineYf, fieldX + dx + 4, lineYf);
            }

            std::string display = m_textInputBuffer;
            if (m_cursorBlink < 0.5f)
                display += "|";
            if (!display.empty()) {
                SDL_Color textCol = {30, 25, 15, static_cast<uint8_t>(240 * eased)};
                SDL_Surface* ts =
                    TTF_RenderText_Blended(font, display.c_str(), display.size(), textCol);
                if (ts) {
                    SDL_Texture* tt = SDL_CreateTextureFromSurface(m_sdlRenderer, ts);
                    float maxTextW = fieldW2 - 16;
                    float drawW = std::min(static_cast<float>(ts->w), maxTextW);
                    SDL_FRect srcRect = {static_cast<float>(ts->w) - drawW, 0, drawW,
                                         static_cast<float>(ts->h)};
                    SDL_FRect tr = {fieldX + 8, fieldY2 + 6, drawW, static_cast<float>(ts->h)};
                    SDL_RenderTexture(m_sdlRenderer, tt, &srcRect, &tr);
                    SDL_DestroyTexture(tt);
                    SDL_DestroySurface(ts);
                }
            }

            float hintY = fieldY2 + fieldH2 + 10;
            const char* hint = "[Enter]  Confirm";
            SDL_Color hintCol = {130, 105, 70, static_cast<uint8_t>(150 * eased)};
            SDL_Surface* hs = TTF_RenderText_Blended(font, hint, strlen(hint), hintCol);
            if (hs) {
                SDL_Texture* ht = SDL_CreateTextureFromSurface(m_sdlRenderer, hs);
                SDL_FRect hr = {cardX + cardW - static_cast<float>(hs->w) - 20, hintY,
                                static_cast<float>(hs->w), static_cast<float>(hs->h)};
                SDL_RenderTexture(m_sdlRenderer, ht, nullptr, &hr);
                SDL_DestroyTexture(ht);
                SDL_DestroySurface(hs);
            }

            SDL_SetRenderClipRect(m_sdlRenderer, nullptr);
        }
    }
}

void Engine::renderScreenElements(const std::vector<script::ScreenElement>& elements, float screenW,
                                  float screenH) {
    for (const auto& el : elements) {
        float px = el.x * screenW;
        float py = el.y * screenH;

        if (el.type == "panel") {
            float w = (el.width > 0) ? el.width * screenW : 200.0f;
            float h = (el.height > 0) ? el.height * screenH : 100.0f;
            uint8_t a = static_cast<uint8_t>(el.alpha * 180.0f);
            SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
            SDL_SetRenderDrawColor(m_sdlRenderer, 14, 20, 18, a);
            SDL_FRect rect = {px, py, w, h};
            SDL_RenderFillRect(m_sdlRenderer, &rect);
            SDL_SetRenderDrawColor(m_sdlRenderer, 92, 118, 101, a);
            SDL_RenderRect(m_sdlRenderer, &rect);
            if (!el.children.empty()) {
                renderScreenElements(el.children, screenW, screenH);
            }
        } else if (el.type == "text") {
            TTF_Font* font = m_richText ? m_richText->getFont() : nullptr;
            if (font && !el.text.empty()) {
                SDL_Color col = {230, 230, 240, 255};
                if (!el.color.empty()) {
                    col = text::RichTextParser::hexToColor(el.color);
                }
                drawText(m_sdlRenderer, font, el.text.c_str(), px, py, col, false);
            }
        } else if (el.type == "imagebutton") {
            float w = (el.width > 0) ? el.width * screenW : 120.0f;
            float h = (el.height > 0) ? el.height * screenH : 40.0f;
            uint8_t a = static_cast<uint8_t>(el.alpha * 200.0f);
            SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
            SDL_FRect rect = {px - w * 0.5f, py - h * 0.5f, w, h};
            SDL_Texture* texture = (!el.imagePath.empty() && m_renderer)
                                       ? m_renderer->loadTexture(el.imagePath)
                                       : nullptr;
            if (texture) {
                SDL_SetTextureAlphaMod(texture, a);
                SDL_RenderTexture(m_sdlRenderer, texture, nullptr, &rect);
                SDL_SetTextureAlphaMod(texture, 255);
            } else {
                SDL_SetRenderDrawColor(m_sdlRenderer, 44, 58, 50, a);
                SDL_RenderFillRect(m_sdlRenderer, &rect);
            }
            SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, a);
            SDL_RenderRect(m_sdlRenderer, &rect);
        }
    }
}

void Engine::renderMainMenu(double alpha) {
    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);
    float fade = m_mainMenuFade;

    TTF_Font* fTitle = m_fontTitle ? m_fontTitle : (m_richText ? m_richText->getFont() : nullptr);
    TTF_Font* fMenu = m_fontMenu ? m_fontMenu : fTitle;
    TTF_Font* fSub = m_fontSub ? m_fontSub : fTitle;
    TTF_Font* fSmall = m_fontSmall ? m_fontSmall : fTitle;
    if (!fTitle)
        return;

    if (m_renderer)
        m_renderer->renderBackground(alpha);
    SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);

    if (m_mainMenuScreen == MainMenuScreen::MAIN) {
        float gradW = w * 0.5f;
        for (int x = 0; x < (int)gradW; x++) {
            float t = static_cast<float>(x) / gradW;
            uint8_t a = static_cast<uint8_t>((1.0f - t * t) * 200.0f * fade);
            SDL_SetRenderDrawColor(m_sdlRenderer, 5, 8, 10, a);
            SDL_FRect strip = {static_cast<float>(x), 0, 1.0f, h};
            SDL_RenderFillRect(m_sdlRenderer, &strip);
        }
        for (int y = (int)(h * 0.8f); y < (int)h; y++) {
            float t = (static_cast<float>(y) - h * 0.8f) / (h * 0.2f);
            uint8_t a = static_cast<uint8_t>(t * 140.0f * fade);
            SDL_SetRenderDrawColor(m_sdlRenderer, 5, 8, 10, a);
            SDL_FRect strip = {0, static_cast<float>(y), w, 1.0f};
            SDL_RenderFillRect(m_sdlRenderer, &strip);
        }
    } else {
        SDL_SetRenderDrawColor(m_sdlRenderer, 8, 10, 14, static_cast<uint8_t>(210 * fade));
        SDL_FRect ov = {0, 0, w, h};
        SDL_RenderFillRect(m_sdlRenderer, &ov);
    }

    uint8_t aFade = static_cast<uint8_t>(255 * fade);
    SDL_Color titleColor = {240, 248, 235, aFade};
    SDL_Color subColor = {172, 204, 157, static_cast<uint8_t>(220 * fade)};
    SDL_Color dimColor = {110, 120, 105, static_cast<uint8_t>(150 * fade)};

    if (m_mainMenuScreen == MainMenuScreen::SAVES) {
        drawText(m_sdlRenderer, fTitle, "Saves", w * 0.5f, 28.0f, titleColor, true);
        auto slots = m_stateManager ? m_stateManager->getSaveSlots()
                                    : std::vector<GameStateManager::SaveSlotInfo>{};
        float slotX = 40, slotW = w * 0.55f, slotH = 52;
        for (int i = 0; i < 9; i++) {
            float sy = 80 + i * (slotH + 8);
            int sn = i + 1;
            bool sel = (m_selectedSaveSlot == sn);
            bool occ = false;
            std::string sLabel, sPrev;
            int64_t ts = 0;
            for (auto& s : slots)
                if (s.slot == sn && s.occupied) {
                    occ = true;
                    sLabel = s.label;
                    sPrev = s.preview;
                    ts = s.timestamp;
                    break;
                }
            SDL_SetRenderDrawColor(m_sdlRenderer, sel ? 35 : 18, sel ? 45 : 20, sel ? 30 : 28, 220);
            SDL_FRect sr = {slotX, sy, slotW, slotH};
            SDL_RenderFillRect(m_sdlRenderer, &sr);
            SDL_SetRenderDrawColor(m_sdlRenderer, sel ? 172 : 60, sel ? 204 : 70, sel ? 157 : 60,
                                   sel ? 255 : 150);
            SDL_RenderRect(m_sdlRenderer, &sr);
            SDL_SetRenderDrawColor(m_sdlRenderer, occ ? 172 : 40, occ ? 204 : 45, occ ? 157 : 50,
                                   occ ? 180 : 80);
            SDL_FRect pv = {slotX + 4, sy + 4, 44, slotH - 8};
            SDL_RenderFillRect(m_sdlRenderer, &pv);
            char nb[8];
            snprintf(nb, sizeof(nb), "%d", sn);
            drawText(m_sdlRenderer, fMenu, nb, slotX + 26, sy + 12, {255, 255, 255, 200}, true);
            if (occ) {
                drawText(m_sdlRenderer, fSub, sLabel.c_str(), slotX + 60, sy + 6,
                         {220, 230, 215, 255}, false);
                std::string prev = sPrev.substr(0, 60);
                if (sPrev.length() > 60)
                    prev += "...";
                drawText(m_sdlRenderer, fSmall, prev.c_str(), slotX + 60, sy + 28,
                         {140, 150, 135, 200}, false);
                if (ts > 0) {
                    if (const auto date = system::formatLocalTime(
                            static_cast<std::time_t>(ts), "%Y-%m-%d %H:%M")) {
                        drawText(m_sdlRenderer, fSmall, date->c_str(), slotX + slotW - 140,
                                 sy + 18, {120, 130, 115, 180}, false);
                    }
                }
            } else {
                drawText(m_sdlRenderer, fSub, "Empty Slot", slotX + 60, sy + 14, {80, 90, 75, 150},
                         false);
            }
        }
        float btnRX = w - 260, btnRW = 220, btnRH = 42;
        bool canLoad = (m_selectedSaveSlot > 0);
        if (canLoad && m_stateManager) {
            auto sl = m_stateManager->getSaveSlots();
            bool f2 = false;
            for (auto& s : sl)
                if (s.slot == m_selectedSaveSlot && s.occupied) {
                    f2 = true;
                    break;
                }
            canLoad = f2;
        }
        float loadY = h - 110;
        SDL_SetRenderDrawColor(m_sdlRenderer, canLoad ? 35 : 20, canLoad ? 55 : 22,
                               canLoad ? 35 : 28, canLoad ? 230 : 100);
        SDL_FRect lb = {btnRX, loadY, btnRW, btnRH};
        SDL_RenderFillRect(m_sdlRenderer, &lb);
        SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, canLoad ? (uint8_t)255 : (uint8_t)60);
        SDL_RenderRect(m_sdlRenderer, &lb);
        drawText(m_sdlRenderer, fSub, "Load Save", btnRX + btnRW * 0.5f, loadY + 12,
                 canLoad ? SDL_Color{220, 240, 210, 255} : SDL_Color{80, 80, 80, 120}, true);
        float backY = h - 58;
        SDL_SetRenderDrawColor(m_sdlRenderer, 22, 20, 30, 220);
        SDL_FRect bb = {btnRX, backY, btnRW, btnRH};
        SDL_RenderFillRect(m_sdlRenderer, &bb);
        SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, 200);
        SDL_RenderRect(m_sdlRenderer, &bb);
        drawText(m_sdlRenderer, fSub, "Back", btnRX + btnRW * 0.5f, backY + 12,
                 {220, 210, 240, 255}, true);
        return;
    }

    if (m_mainMenuScreen == MainMenuScreen::SETTINGS) {
        renderGameMenu(alpha);
        if (m_confirmActive) {
            SDL_SetRenderDrawColor(m_sdlRenderer, 0, 0, 0, 160);
            SDL_FRect ovr = {0, 0, w, h};
            SDL_RenderFillRect(m_sdlRenderer, &ovr);
            float dw = 420, dh = 170, dx = w * 0.5f - 210, dy = h * 0.5f - 85;
            SDL_SetRenderDrawColor(m_sdlRenderer, 18, 16, 28, 245);
            SDL_FRect dlg = {dx, dy, dw, dh};
            SDL_RenderFillRect(m_sdlRenderer, &dlg);
            SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, 200);
            SDL_RenderRect(m_sdlRenderer, &dlg);
            drawText(m_sdlRenderer, fSub, "Save changes?", w * 0.5f, dy + 35, {220, 230, 215, 255},
                     true);
            float bW = 120, bH = 36, bY = dy + dh - 55;
            const char* bl[] = {"Yes", "No", "Cancel"};
            float ps[] = {dx + 30, dx + dw / 2 - bW / 2, dx + dw - 30 - bW};
            for (int i = 0; i < 3; i++) {
                SDL_SetRenderDrawColor(m_sdlRenderer, 25, 30, 25, 230);
                SDL_FRect b = {ps[i], bY, bW, bH};
                SDL_RenderFillRect(m_sdlRenderer, &b);
                SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, 180);
                SDL_RenderRect(m_sdlRenderer, &b);
                drawText(m_sdlRenderer, fSub, bl[i], ps[i] + bW * 0.5f, bY + 9,
                         {220, 230, 215, 255}, true);
            }
        }
        return;
    }

    if (m_mainMenuScreen == MainMenuScreen::CREDITS) {
        drawText(m_sdlRenderer, fTitle, "About Velora", w * 0.5f, 28.0f, titleColor, true);
        float cy = h * 0.22f, lh = 34.0f;
        drawText(m_sdlRenderer, fSub, "Runtime", w * 0.5f, cy, subColor, true);
        drawText(m_sdlRenderer, fMenu, "Velora Engine", w * 0.5f, cy + lh, titleColor, true);
        cy += lh * 2.5f;
        drawText(m_sdlRenderer, fSub, "Scripting language", w * 0.5f, cy, subColor, true);
        drawText(m_sdlRenderer, fMenu, "VelScript", w * 0.5f, cy + lh, titleColor, true);
        cy += lh * 2.5f;
        drawText(m_sdlRenderer, fSub, "Core libraries", w * 0.5f, cy, subColor, true);
        drawText(m_sdlRenderer, fMenu, "SDL 3  /  miniaudio  /  nlohmann JSON", w * 0.5f, cy + lh,
                 titleColor, true);
        cy += lh * 2.5f;
        drawText(m_sdlRenderer, fSub, "Version", w * 0.5f, cy, subColor, true);
        drawText(m_sdlRenderer, fMenu, VELORA_VERSION, w * 0.5f, cy + lh, titleColor, true);
        float backY = h - 58, btnRX = w - 260, btnRW = 220, btnRH = 42;
        SDL_SetRenderDrawColor(m_sdlRenderer, 22, 20, 30, 220);
        SDL_FRect bb = {btnRX, backY, btnRW, btnRH};
        SDL_RenderFillRect(m_sdlRenderer, &bb);
        SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, 200);
        SDL_RenderRect(m_sdlRenderer, &bb);
        drawText(m_sdlRenderer, fSub, "Back", btnRX + btnRW * 0.5f, backY + 12,
                 {220, 210, 240, 255}, true);
        return;
    }

    float leftMargin = w * 0.06f;
    float titleY = h * 0.12f + m_mainMenuTitleY;
    drawText(m_sdlRenderer, fTitle, "VELORA", leftMargin, titleY, titleColor, false);
    drawText(m_sdlRenderer, fSub, "VelScript Runtime", leftMargin + 3.0f, titleY + 54.0f, subColor,
             false);
    float lineYm = titleY + 84.0f;
    SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157, static_cast<uint8_t>(80 * fade));
    SDL_FRect div = {leftMargin, lineYm, 260.0f, 2.0f};
    SDL_RenderFillRect(m_sdlRenderer, &div);

    float btnX = leftMargin, btnW = 320.0f, btnH = 50.0f;
    float btnStartY = h * 0.38f, btnSpacing = 56.0f;
    bool hasSave = false;
    if (m_stateManager) {
        auto sl = m_stateManager->getSaveSlots();
        for (auto& s : sl)
            if (s.occupied) {
                hasSave = true;
                break;
            }
    }
    struct MenuBtn {
        const char* label;
        int action;
    };
    MenuBtn buttons[8];
    int btnCount = 0;
    if (hasSave)
        buttons[btnCount++] = {"Continue", 0};
    buttons[btnCount++] = {"New Game", 1};
    buttons[btnCount++] = {"Saves", 2};
    buttons[btnCount++] = {"Settings", 3};
    buttons[btnCount++] = {"Credits", 4};
    buttons[btnCount++] = {"Exit", 5};

    for (int i = 0; i < btnCount; i++) {
        float by = btnStartY + i * btnSpacing;
        float anim = m_mainMenuBtnAnim[i];
        float bf = fade - (i * 0.06f);
        if (bf < 0)
            bf = 0;
        if (bf > 1)
            bf = 1;
        if (anim > 0.01f) {
            SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157,
                                   static_cast<uint8_t>(200 * anim * bf));
            SDL_FRect ac = {btnX - 6, by + 8, 3, btnH - 16};
            SDL_RenderFillRect(m_sdlRenderer, &ac);
            SDL_SetRenderDrawColor(m_sdlRenderer, 172, 204, 157,
                                   static_cast<uint8_t>(20 * anim * bf));
            SDL_FRect bg = {btnX, by, btnW, btnH};
            SDL_RenderFillRect(m_sdlRenderer, &bg);
        }
        float ox = anim * 10.0f;
        SDL_Color tc;
        if (anim > 0.5f)
            tc = {172, 204, 157, static_cast<uint8_t>(255 * bf)};
        else {
            uint8_t v = static_cast<uint8_t>(200 + anim * 55);
            tc = {v, v, static_cast<uint8_t>(v - 15), static_cast<uint8_t>(255 * bf)};
        }
        drawText(m_sdlRenderer, fMenu, buttons[i].label, btnX + 16 + ox, by + 10, tc, false);
    }
    const std::string version = std::string("v") + VELORA_VERSION;
    drawText(m_sdlRenderer, fSmall, version.c_str(), w - 70, h - 44, dimColor, false);
    drawText(m_sdlRenderer, fSmall, "Velora Engine", w - 140, h - 24, dimColor, false);
}

void Engine::renderPaused(double alpha) {
    (void)alpha;
    SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
    SDL_SetRenderDrawColor(m_sdlRenderer, 0, 0, 0, 128);
    SDL_FRect ov = {0, 0, (float)DESIGN_W, (float)DESIGN_H};
    SDL_RenderFillRect(m_sdlRenderer, &ov);
}

void Engine::renderInMenu(double alpha) {
    (void)alpha;
}

static void drawSlider(SDL_Renderer* r, TTF_Font* font, const char* label, float x, float y,
                       float slW, float val) {
    if (font)
        drawText(r, font, label, x, y - 2, {190, 180, 210, 255}, false);
    float trackY = y + 24, trackH = 8;
    SDL_SetRenderDrawColor(r, 25, 20, 40, 200);
    SDL_FRect track = {x, trackY, slW, trackH};
    SDL_RenderFillRect(r, &track);
    SDL_SetRenderDrawColor(r, 130, 90, 210, 200);
    SDL_FRect fill = {x, trackY, slW * val, trackH};
    SDL_RenderFillRect(r, &fill);
    float hx = x + slW * val - 5;
    SDL_SetRenderDrawColor(r, 220, 200, 255, 255);
    SDL_FRect handle = {hx, trackY - 4, 10, trackH + 8};
    SDL_RenderFillRect(r, &handle);
    SDL_SetRenderDrawColor(r, 90, 70, 140, 150);
    SDL_RenderRect(r, &track);
}

void Engine::renderQuickBar(double alpha) {
    (void)alpha;
    if (!m_richText || !m_richText->isActive())
        return;
    TTF_Font* font = m_richText->getFont();

    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);
    float barH = 28.0f;
    float barY = h - barH;

    SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
    SDL_SetRenderDrawColor(m_sdlRenderer, 10, 8, 20, 200);
    SDL_FRect bar = {0, barY, w, barH};
    SDL_RenderFillRect(m_sdlRenderer, &bar);
    SDL_Color qac = m_richText->getAccentColor();
    SDL_SetRenderDrawColor(m_sdlRenderer, qac.r, qac.g, qac.b, 120);
    SDL_RenderLine(m_sdlRenderer, 0, barY, w, barY);

    if (!font)
        return;

    const char* labels[] = {
        "Menu", "Save", "Load", "Auto", "Skip", "History", "Settings", "Hide",
    };
    bool active[] = {false, false, false, m_autoMode, m_skipMode, m_showBacklog, false, false};
    const int N = 8;
    float spacing = 28.0f;
    float cy = barY + barH * 0.5f;

    float totalW = 0;
    float itemWidths[8];
    for (int i = 0; i < N; i++) {
        size_t len = strlen(labels[i]);
        SDL_Surface* tmp = TTF_RenderText_Blended(font, labels[i], len, {255, 255, 255, 255});
        itemWidths[i] = tmp ? static_cast<float>(tmp->w) : 50.0f;
        if (tmp)
            SDL_DestroySurface(tmp);
        totalW += itemWidths[i];
    }
    totalW += (N - 1) * spacing;
    float cx = (w - totalW) * 0.5f;

    for (int i = 0; i < N; i++) {
        SDL_Color tc;
        if (active[i]) {
            tc = {static_cast<uint8_t>(std::min(255, qac.r + 100)),
                  static_cast<uint8_t>(std::min(255, qac.g + 100)),
                  static_cast<uint8_t>(std::min(255, qac.b + 100)), 255};
        } else {
            tc = {160, 145, 190, 220};
        }
        drawText(m_sdlRenderer, font, labels[i], cx + itemWidths[i] * 0.5f, cy, tc);

        if (i < N - 1) {
            float dotX = cx + itemWidths[i] + spacing * 0.5f;
            SDL_SetRenderDrawColor(m_sdlRenderer, 100, 80, 150, 100);
            SDL_FRect dot = {dotX - 1.5f, cy - 1.5f, 3.0f, 3.0f};
            SDL_RenderFillRect(m_sdlRenderer, &dot);
        }

        cx += itemWidths[i] + spacing;
    }
}

void Engine::renderGameMenu(double alpha) {
    (void)alpha;
    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);
    TTF_Font* font = m_richText ? m_richText->getFont() : nullptr;
    SDL_Color ac = m_richText ? m_richText->getAccentColor() : SDL_Color{76, 175, 80, 255};

    SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
    SDL_SetRenderDrawColor(m_sdlRenderer, 8, 6, 18, 220);
    SDL_FRect ov = {0, 0, w, h};
    SDL_RenderFillRect(m_sdlRenderer, &ov);

    SDL_SetRenderDrawColor(m_sdlRenderer, static_cast<uint8_t>(ac.r / 3),
                           static_cast<uint8_t>(ac.g / 5), static_cast<uint8_t>(ac.b / 3), 180);
    SDL_FRect titleBar = {0, 0, w, 55};
    SDL_RenderFillRect(m_sdlRenderer, &titleBar);
    SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 200);
    SDL_RenderLine(m_sdlRenderer, 0, 55, w, 55);

    if (m_mainMenuScreen == MainMenuScreen::SETTINGS) {
        if (font)
            drawText(m_sdlRenderer, font, "Settings", w * 0.5f, 27, {255, 240, 255, 255});
    } else {
        const char* tabTitle = (m_gameMenuTab == GameMenuTab::SETTINGS) ? "Settings" : "Pause";
        if (font)
            drawText(m_sdlRenderer, font, tabTitle, w * 0.5f, 27, {255, 240, 255, 255});
    }

    if (m_gameMenuTab == GameMenuTab::MAIN) {
        const char* labels[] = {
            "Resume", "Save", "Load", "Settings", nullptr, "Main Menu", "Exit Game",
        };
        float btnW2 = 300, btnH2 = 44, gap2 = 10;
        int btnTotal = 7;
        float totalH2 = btnTotal * btnH2 + (btnTotal - 1) * gap2;
        float sy = (h - totalH2) * 0.5f + 10;
        float sx = (w - btnW2) * 0.5f;
        for (int i = 0; i < btnTotal; i++) {
            float by = sy + i * (btnH2 + gap2);
            if (labels[i] == nullptr) {
                SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 60);
                SDL_FRect sep = {sx + 20, by + btnH2 * 0.5f - 1, btnW2 - 40, 1.5f};
                SDL_RenderFillRect(m_sdlRenderer, &sep);
                continue;
            }
            if (i >= 5) {
                SDL_SetRenderDrawColor(m_sdlRenderer, 48, 22, 28, 230);
            } else {
                SDL_SetRenderDrawColor(m_sdlRenderer, 28, 22, 48, 230);
            }
            SDL_FRect btn = {sx, by, btnW2, btnH2};
            SDL_RenderFillRect(m_sdlRenderer, &btn);
            SDL_SetRenderDrawColor(m_sdlRenderer, i >= 5 ? (uint8_t)180 : ac.r,
                                   i >= 5 ? (uint8_t)100 : ac.g, i >= 5 ? (uint8_t)100 : ac.b, 200);
            SDL_RenderRect(m_sdlRenderer, &btn);
            SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 255);
            SDL_FRect acBar = {sx, by, 4, btnH2};
            SDL_RenderFillRect(m_sdlRenderer, &acBar);
            SDL_Color textColor =
                i >= 5 ? SDL_Color{240, 180, 180, 255} : SDL_Color{220, 210, 240, 255};
            if (font)
                drawText(m_sdlRenderer, font, labels[i], sx + btnW2 * 0.5f, by + btnH2 * 0.5f,
                         textColor);
        }
        if (font) {
            float rx = w - 190;
            drawText(m_sdlRenderer, font, "Shortcuts", rx + 70, 80, {170, 150, 210, 210});
            const char* sc[] = {"Esc", "F5", "F7", "A", "Ctrl", "H"};
            static const char* descriptions[] = {"Resume", "Save", "Load", "Auto", "Skip", "Hide"};
            for (int i = 0; i < 6; i++) {
                char buf[64];
                snprintf(buf, sizeof(buf), "%-5s %s", sc[i], descriptions[i]);
                drawText(m_sdlRenderer, font, buf, rx + 70, 115 + i * 26, {130, 115, 160, 170});
            }
        }
    } else if (m_gameMenuTab == GameMenuTab::SETTINGS) {
        float px = 80, py = 85, slW = 280;
        drawSlider(m_sdlRenderer, font, "Text Speed", px, py, slW, m_textSpeed / 60.0f);
        drawSlider(m_sdlRenderer, font, "Auto Delay", px, py + 65, slW, m_autoDelay / 5.0f);
        drawSlider(m_sdlRenderer, font, "Skip Speed", px, py + 130, slW, m_skipSpeed / 10.0f);
        drawSlider(m_sdlRenderer, font, "Music Volume", px + 400, py, slW, m_musicVolume);
        drawSlider(m_sdlRenderer, font, "Sound Volume", px + 400, py + 65, slW, m_soundVolume);

        float tgY = py + 210;
        bool se = m_pendingSkipEffects;
        bool seChanged = (m_pendingSkipEffects != m_skipEffects);
        SDL_SetRenderDrawColor(m_sdlRenderer,
                               se ? static_cast<uint8_t>(ac.r / 2) : (seChanged ? 40 : 30),
                               se ? static_cast<uint8_t>(ac.g / 3) : (seChanged ? 32 : 22),
                               se ? static_cast<uint8_t>(ac.b / 2) : (seChanged ? 20 : 48), 230);
        SDL_FRect seBtn = {px, tgY, 340, 38};
        SDL_RenderFillRect(m_sdlRenderer, &seBtn);
        SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 200);
        SDL_RenderRect(m_sdlRenderer, &seBtn);
        if (font)
            drawText(m_sdlRenderer, font, se ? "Skip Effects: ON" : "Skip Effects: OFF", px + 170,
                     tgY + 19, {220, 210, 240, 255});

        float tgY2 = tgY + 50;
        const char* modeLabels[] = {"Windowed", "Borderless", "Fullscreen"};
        bool dmChanged = (m_pendingDisplayMode != m_displayMode);
        SDL_SetRenderDrawColor(m_sdlRenderer, dmChanged ? 40 : 30, dmChanged ? 32 : 22,
                               dmChanged ? 20 : 48, 230);
        SDL_FRect dmBtn = {px, tgY2, 320, 38};
        SDL_RenderFillRect(m_sdlRenderer, &dmBtn);
        SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 200);
        SDL_RenderRect(m_sdlRenderer, &dmBtn);
        char dmBuf[128];
        snprintf(dmBuf, sizeof(dmBuf), "%s: %s", "Display", modeLabels[m_pendingDisplayMode % 3]);
        if (font)
            drawText(m_sdlRenderer, font, dmBuf, px + 160, tgY2 + 19, {220, 210, 240, 255});

        float tgY3 = tgY2 + 50;
        const char* resLabels[] = {"1280x720", "1366x768", "1600x900", "1920x1080"};
        bool resChanged = false;
        {
            int rw[] = {1280, 1366, 1600, 1920};
            int rh[] = {720, 768, 900, 1080};
            int curReal = 0;
            for (int i = 0; i < 4; i++)
                if (m_config.windowWidth == rw[i] && m_config.windowHeight == rh[i])
                    curReal = i;
            resChanged = (m_pendingResIndex != curReal);
        }
        SDL_SetRenderDrawColor(m_sdlRenderer, resChanged ? 40 : 30, resChanged ? 32 : 22,
                               resChanged ? 20 : 48, 230);
        SDL_FRect resBtn = {px, tgY3, 320, 38};
        SDL_RenderFillRect(m_sdlRenderer, &resBtn);
        SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 200);
        SDL_RenderRect(m_sdlRenderer, &resBtn);
        char resBuf[128];
        snprintf(resBuf, sizeof(resBuf), "%s: %s", "Resolution", resLabels[m_pendingResIndex % 4]);
        if (font)
            drawText(m_sdlRenderer, font, resBuf, px + 160, tgY3 + 19, {220, 210, 240, 255});

        m_settingsChanged = dmChanged || resChanged || (m_pendingSkipEffects != m_skipEffects);

        float btnY = h - 65;
        if (m_settingsChanged) {
            SDL_SetRenderDrawColor(m_sdlRenderer, 45, 70, 40, 240);
        } else {
            SDL_SetRenderDrawColor(m_sdlRenderer, 28, 22, 48, 150);
        }
        SDL_FRect applyBtn = {px, btnY, 200, 40};
        SDL_RenderFillRect(m_sdlRenderer, &applyBtn);
        SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b,
                               m_settingsChanged ? (uint8_t)255 : (uint8_t)100);
        SDL_RenderRect(m_sdlRenderer, &applyBtn);
        if (font)
            drawText(m_sdlRenderer, font, "Apply", px + 100, btnY + 20,
                     m_settingsChanged ? SDL_Color{220, 240, 210, 255}
                                       : SDL_Color{120, 110, 130, 180});

        SDL_SetRenderDrawColor(m_sdlRenderer, 28, 22, 48, 230);
        SDL_FRect retBtn = {px + 220, btnY, 200, 40};
        SDL_RenderFillRect(m_sdlRenderer, &retBtn);
        SDL_SetRenderDrawColor(m_sdlRenderer, ac.r, ac.g, ac.b, 200);
        SDL_RenderRect(m_sdlRenderer, &retBtn);
        if (font)
            drawText(m_sdlRenderer, font, "Back", px + 320, btnY + 20, {220, 210, 240, 255});
    }
}

void Engine::handleGameMenuClick(float x, float y) {
    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);

    if (m_gameMenuTab == GameMenuTab::MAIN) {
        float btnW2 = 300, btnH2 = 44, gap2 = 10;
        int btnTotal = 7;
        float totalH2 = btnTotal * btnH2 + (btnTotal - 1) * gap2;
        float sy = (h - totalH2) * 0.5f + 10;
        float sx = (w - btnW2) * 0.5f;
        for (int i = 0; i < btnTotal; i++) {
            if (i == 4)
                continue;
            float by = sy + i * (btnH2 + gap2);
            if (x >= sx && x <= sx + btnW2 && y >= by && y <= by + btnH2) {
                switch (i) {
                case 0:
                    requestStateChange(EngineState::PLAYING);
                    break;
                case 1:
                    if (m_stateManager)
                        m_stateManager->quickSave();
                    m_statusMessage = "Saved";
                    m_statusTimer = 2.0f;
                    requestStateChange(EngineState::PLAYING);
                    break;
                case 2:
                    if (m_stateManager) {
                        m_stateManager->quickLoad();
                        restoreFromSnapshot();
                    }
                    m_statusMessage = "Loaded";
                    m_statusTimer = 2.0f;
                    requestStateChange(EngineState::PLAYING);
                    break;
                case 3:
                    m_gameMenuTab = GameMenuTab::SETTINGS;
                    m_pendingDisplayMode = m_displayMode;
                    m_pendingLocale = m_locale;
                    m_pendingSkipEffects = m_skipEffects;
                    {
                        int rw[] = {1280, 1366, 1600, 1920};
                        int rh[] = {720, 768, 900, 1080};
                        m_pendingResIndex = 0;
                        for (int j = 0; j < 4; j++)
                            if (m_config.windowWidth == rw[j] && m_config.windowHeight == rh[j])
                                m_pendingResIndex = j;
                    }
                    m_settingsChanged = false;
                    break;
                case 5:
                    m_state = EngineState::MAIN_MENU;
                    m_mainMenuScreen = MainMenuScreen::MAIN;
                    m_mainMenuFade = 0.0f;
                    m_mainMenuTitleY = -30.0f;
                    m_mainMenuHover = -1;
                    break;
                case 6:
                    m_running = false;
                    break;
                }
                return;
            }
        }
    } else if (m_gameMenuTab == GameMenuTab::SETTINGS) {
        float px = 80, py = 85, slW = 280;
        struct {
            float sx, sy;
            float* val;
            float maxV;
        } sl[] = {
            {px, py + 24 + 24, &m_textSpeed, 60.0f},
            {px, py + 65 + 24, &m_autoDelay, 5.0f},
            {px, py + 130 + 24, &m_skipSpeed, 10.0f},
            {px + 400, py + 24, &m_musicVolume, 1.0f},
            {px + 400, py + 65 + 24, &m_soundVolume, 1.0f},
        };
        for (int i = 0; i < 5; i++) {
            if (x >= sl[i].sx && x <= sl[i].sx + slW && y >= sl[i].sy - 6 && y <= sl[i].sy + 14) {
                float n = (x - sl[i].sx) / slW;
                if (n < 0.02f)
                    n = 0.02f;
                if (n > 1)
                    n = 1;
                *sl[i].val = n * sl[i].maxV;
                m_settingsChanged = true;
                if (sl[i].val == &m_textSpeed && m_richText)
                    m_richText->setCharactersPerSecond(m_textSpeed);
                return;
            }
        }
        float tgY = py + 210;
        if (x >= px && x <= px + 340 && y >= tgY && y <= tgY + 38) {
            m_pendingSkipEffects = !m_pendingSkipEffects;
            m_settingsChanged = true;
            return;
        }
        float tgY2 = tgY + 50;
        if (x >= px && x <= px + 320 && y >= tgY2 && y <= tgY2 + 38) {
            m_pendingDisplayMode = (m_pendingDisplayMode + 1) % 3;
            m_settingsChanged = true;
            return;
        }
        float tgY3 = tgY2 + 50;
        if (x >= px && x <= px + 320 && y >= tgY3 && y <= tgY3 + 38) {
            m_pendingResIndex = (m_pendingResIndex + 1) % 4;
            m_settingsChanged = true;
            return;
        }
        float btnY = h - 65;
        if (x >= px && x <= px + 200 && y >= btnY && y <= btnY + 40) {
            if (m_settingsChanged) {
                applySettings();
                m_settingsChanged = false;
                m_statusMessage = "Settings applied";
                m_statusTimer = 2.0f;
                saveSettings();
            }
            return;
        }
        if (x >= px + 220 && x <= px + 420 && y >= btnY && y <= btnY + 40) {
            if (m_mainMenuScreen == MainMenuScreen::SETTINGS) {
                if (m_settingsChanged) {
                    m_confirmActive = true;
                } else {
                    m_mainMenuScreen = MainMenuScreen::MAIN;
                }
            } else {
                if (m_settingsChanged) {
                    m_confirmActive = true;
                } else {
                    m_gameMenuTab = GameMenuTab::MAIN;
                }
            }
            return;
        }
    }
}

void Engine::renderBacklog(double alpha) {
    (void)alpha;
    float w = static_cast<float>(DESIGN_W);
    float h = static_cast<float>(DESIGN_H);
    TTF_Font* font = m_richText ? m_richText->getFont() : nullptr;
    if (!font || m_dialogueHistory.empty())
        return;

    SDL_SetRenderDrawBlendMode(m_sdlRenderer, SDL_BLENDMODE_BLEND);
    SDL_SetRenderDrawColor(m_sdlRenderer, 5, 3, 15, 235);
    SDL_FRect ov = {0, 0, w, h};
    SDL_RenderFillRect(m_sdlRenderer, &ov);

    SDL_SetRenderDrawColor(m_sdlRenderer, 40, 25, 80, 200);
    SDL_FRect titleBar = {0, 0, w, 50};
    SDL_RenderFillRect(m_sdlRenderer, &titleBar);
    SDL_SetRenderDrawColor(m_sdlRenderer, 130, 90, 210, 180);
    SDL_RenderLine(m_sdlRenderer, 0, 50, w, 50);

    const char* title = "History  (Scroll / Page Up to close)";
    drawText(m_sdlRenderer, font, title, w * 0.5f, 25, {200, 180, 240, 255});

    float margin = 40.0f;
    float entryPadding = 12.0f;
    float maxTextW = w - margin * 2 - 20;
    float lineH = 28.0f;

    float totalContentH = 0;
    for (size_t i = 0; i < m_dialogueHistory.size(); i++) {
        totalContentH += lineH;
        float charW = 12.0f;
        int charsPerLine = static_cast<int>(maxTextW / charW);
        if (charsPerLine < 10)
            charsPerLine = 10;
        int textLines = 1;
        if (!m_dialogueHistory[i].text.empty()) {
            int textLen = static_cast<int>(m_dialogueHistory[i].text.size());
            textLines = (textLen / charsPerLine) + 1;
        }
        totalContentH += textLines * lineH;
        totalContentH += entryPadding;
    }

    float viewH = h - 60;
    float maxScroll = totalContentH - viewH;
    if (maxScroll < 0)
        maxScroll = 0;
    if (m_backlogScroll > maxScroll)
        m_backlogScroll = maxScroll;

    float startY = h - 20 + m_backlogScroll;

    for (int i = static_cast<int>(m_dialogueHistory.size()) - 1; i >= 0; i--) {
        const auto& entry = m_dialogueHistory[i];

        float charW = 12.0f;
        int charsPerLine = static_cast<int>(maxTextW / charW);
        if (charsPerLine < 10)
            charsPerLine = 10;
        int textLines = 1;
        if (!entry.text.empty()) {
            int textLen = static_cast<int>(entry.text.size());
            textLines = (textLen / charsPerLine) + 1;
        }
        float entryH = lineH + textLines * lineH + entryPadding;
        startY -= entryH;

        if (startY + entryH < 55)
            continue;
        if (startY > h)
            continue;

        SDL_Color speakerColor = {180, 180, 200, 255};
        if (entry.speakerColor.size() == 7 && entry.speakerColor[0] == '#') {
            unsigned int r, g, b;
            if (sscanf(entry.speakerColor.c_str(), "#%02x%02x%02x", &r, &g, &b) == 3) {
                speakerColor = {static_cast<uint8_t>(r), static_cast<uint8_t>(g),
                                static_cast<uint8_t>(b), 255};
            }
        }

        float cx = margin;
        float cy = startY;
        if (entry.speaker.empty()) {
            drawText(m_sdlRenderer, font, entry.text.c_str(), cx, cy + lineH * 0.5f,
                     {190, 195, 205, 230}, false);
        } else {
            drawText(m_sdlRenderer, font, entry.speaker.c_str(), cx, cy + lineH * 0.5f,
                     speakerColor, false);
            cy += lineH;
            drawText(m_sdlRenderer, font, entry.text.c_str(), cx + 20, cy + lineH * 0.5f,
                     {230, 230, 235, 240}, false);
        }

        float sepY = startY + entryH - entryPadding * 0.5f;
        if (sepY > 55 && sepY < h - 5) {
            SDL_SetRenderDrawColor(m_sdlRenderer, 74, 94, 82, 80);
            SDL_RenderLine(m_sdlRenderer, margin, sepY, w - margin, sepY);
        }
    }
}

bool Engine::isTextInputMode() const {
    return m_textInputActive ||
           (m_scriptVM && m_scriptVM->getState() == script::VMState::WAITING_TEXT_INPUT);
}

void Engine::applySettings() {
    if (m_settings) {
        m_settings->pendingDisplayMode = m_pendingDisplayMode;
        m_settings->pendingResIndex = m_pendingResIndex;
        m_settings->pendingLocale = m_pendingLocale;
        m_settings->pendingSkipEffects = m_pendingSkipEffects;
        m_settings->apply(m_window, m_sdlRenderer, m_config);
        m_displayMode = m_settings->displayMode;
        m_isFullscreen = m_settings->isFullscreen;
        m_locale = m_settings->locale;
        m_skipEffects = m_settings->skipEffects;
        if (m_richText)
            m_richText->setCharactersPerSecond(m_textSpeed);
        if (m_audioMixer) {
            m_audioMixer->setBGMVolume(m_musicVolume);
            m_audioMixer->setSFXVolume(m_soundVolume);
        }
        if (m_localization)
            m_localization->setLocale(m_locale);
    }
}

void Engine::saveSettings() {
    if (m_settings) {
        m_settings->displayMode = m_displayMode;
        m_settings->isFullscreen = m_isFullscreen;
        m_settings->locale = m_locale;
        m_settings->musicVolume = m_musicVolume;
        m_settings->soundVolume = m_soundVolume;
        m_settings->textSpeed = m_textSpeed;
        m_settings->autoDelay = m_autoDelay;
        m_settings->skipSpeed = m_skipSpeed;
        m_settings->skipEffects = m_skipEffects;
        m_settings->save();
    }
}

void Engine::loadSettings() {
    if (m_settings) {
        m_settings->load();
        m_displayMode = m_settings->displayMode;
        m_isFullscreen = m_settings->isFullscreen;
        m_locale = m_settings->locale;
        m_musicVolume = m_settings->musicVolume;
        m_soundVolume = m_settings->soundVolume;
        m_textSpeed = m_settings->textSpeed;
        m_autoDelay = m_settings->autoDelay;
        m_skipSpeed = m_settings->skipSpeed;
        m_skipEffects = m_settings->skipEffects;
    }
}
} // namespace velora::core
