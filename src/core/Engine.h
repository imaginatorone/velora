
#pragma once

#include "SettingsManager.h"
#include "render/PostEffects.h"

#include <SDL3/SDL.h>
#include <SDL3_ttf/SDL_ttf.h>
#include <string>
#include <vector>
#include <memory>
#include <functional>

#include "script/Parser.h"
namespace velora::script {
class Lexer;
class ScriptVM;
} // namespace velora::script
namespace velora::render {
class Renderer;
class TweenManager;
class ParticleSystem;
} // namespace velora::render
namespace velora::audio {
class AudioMixer;
}
namespace velora::text {
class RichTextParser;
}
namespace velora::asset {
class AssetManager;
}
namespace velora::i18n {
class LocalizationManager;
}

namespace velora::core {

class TimeManager;
class GameStateManager;

struct EngineConfig {
    std::string windowTitle = "Velora Engine";
    int windowWidth = 1920;
    int windowHeight = 1080;
    bool fullscreen = false;
    bool debugMode = false;
    std::string scriptPath = "assets/scripts/example.vel";
    int targetFPS = 60;
    bool overrideWindowSize = false;
    std::string locale = "en";
    std::string assetRoot = "assets";
};

constexpr int DESIGN_W = 1280;
constexpr int DESIGN_H = 720;

enum class EngineState {
    SPLASH_SCREEN,
    INITIALIZING,
    MAIN_MENU,
    PLAYING,
    PAUSED,
    GAME_MENU,
    IN_MENU,
    TRANSITIONING,
    SHUTTING_DOWN,
};

enum class GameMenuTab {
    MAIN,
    SAVE,
    LOAD,
    SETTINGS,
    LOG,
};

class Engine {
  public:
    Engine();
    ~Engine();

    Engine(const Engine&) = delete;
    Engine& operator=(const Engine&) = delete;
    Engine(Engine&&) = delete;
    Engine& operator=(Engine&&) = delete;

    struct DialogueEntry {
        std::string speaker;
        std::string speakerColor;
        std::string text;
    };
    static constexpr size_t MAX_HISTORY = 500;

    [[nodiscard]] bool initialize(const EngineConfig& config);

    void run();

    void shutdown();

    [[nodiscard]] EngineState getState() const { return m_state; }

    void requestStateChange(EngineState newState);

    [[nodiscard]] bool isRunning() const { return m_running; }

    void requestQuit() { m_running = false; }

    [[nodiscard]] TimeManager* getTimeManager() const { return m_timeManager.get(); }
    [[nodiscard]] GameStateManager* getStateManager() const { return m_stateManager.get(); }
    [[nodiscard]] script::ScriptVM* getScriptVM() const { return m_scriptVM.get(); }
    [[nodiscard]] render::Renderer* getRenderer() const { return m_renderer.get(); }
    [[nodiscard]] render::TweenManager* getTweenManager() const { return m_tweenManager.get(); }
    [[nodiscard]] audio::AudioMixer* getAudioMixer() const { return m_audioMixer.get(); }
    [[nodiscard]] text::RichTextParser* getRichText() const { return m_richText.get(); }
    [[nodiscard]] asset::AssetManager* getAssetManager() const { return m_assetManager.get(); }
    [[nodiscard]] i18n::LocalizationManager* getLocalization() const {
        return m_localization.get();
    }
    [[nodiscard]] render::ParticleSystem* getParticles() const { return m_particles.get(); }
    [[nodiscard]] SettingsManager* getSettings() const { return m_settings.get(); }
    [[nodiscard]] PostEffects* getPostEffects() const { return m_postEffects.get(); }

    [[nodiscard]] const EngineConfig& getConfig() const { return m_config; }
    [[nodiscard]] bool isReplaying() const { return m_replaying; }
    [[nodiscard]] SDL_Window* getWindow() const { return m_window; }
    [[nodiscard]] SDL_Renderer* getSDLRenderer() const { return m_sdlRenderer; }

    [[nodiscard]] std::vector<DialogueEntry>& getDialogueHistory() { return m_dialogueHistory; }

    void setSaveLocked(bool locked) { m_saveLocked = locked; }
    [[nodiscard]] bool isSaveLocked() const { return m_saveLocked; }
    void setQuickbarVisible(bool vis) { m_quickbarVisible = vis; }
    void startGlitchText(double duration) { m_glitchTextTimer = duration; }
    void startGlitchUI(double duration) { m_glitchUITimer = duration; }
    void startWindowShake(double duration) {
        SDL_GetWindowPosition(m_window, &m_windowOrigX, &m_windowOrigY);
        m_windowShakeTimer = duration;
    }

    [[nodiscard]] bool isTextInputMode() const;

  private:
    void processEvents();

    void update(double deltaTime);

    void render(double alpha);

    void updatePlaying(double dt);
    void updateMainMenu(double dt);
    void updatePaused(double dt);
    void updateInMenu(double dt);

    void renderPlaying(double alpha);
    void renderMainMenu(double alpha);
    void renderPaused(double alpha);
    void renderInMenu(double alpha);
    void renderGameMenu(double alpha);
    void renderQuickBar(double alpha);

    void handleKeyDown(const SDL_KeyboardEvent& event);
    void handleMouseDown(const SDL_MouseButtonEvent& event);
    void handleMouseWheel(const SDL_MouseWheelEvent& event);
    void handleGameMenuClick(float x, float y);

    void advanceDialogue();

    void restoreFromSnapshot();

    void renderBacklog(double alpha);

    void hotReload();

    void renderScreenElements(const std::vector<script::ScreenElement>& elements, float screenW,
                              float screenH);

    void applySettings();

    void saveSettings();

    void loadSettings();

    SDL_Window* m_window = nullptr;
    SDL_Renderer* m_sdlRenderer = nullptr;

    TTF_Font* m_fontTitle = nullptr;
    TTF_Font* m_fontMenu = nullptr;
    TTF_Font* m_fontSub = nullptr;
    TTF_Font* m_fontSmall = nullptr;

    EngineConfig m_config;
    EngineState m_state = EngineState::INITIALIZING;
    bool m_running = false;
    bool m_sdlInitialized = false;
    bool m_ttfInitialized = false;

    GameMenuTab m_gameMenuTab = GameMenuTab::MAIN;
    bool m_autoMode = false;
    float m_autoTimer = 0.0f;
    bool m_skipMode = false;
    bool m_hideUI = false;
    std::string m_statusMessage;
    float m_statusTimer = 0.0f;

    std::vector<DialogueEntry> m_dialogueHistory;
    bool m_showBacklog = false;
    float m_backlogScroll = 0.0f;

    float m_textSpeed = 30.0f;
    float m_autoDelay = 1.5f;
    float m_musicVolume = 0.8f;
    float m_soundVolume = 0.8f;
    bool m_isFullscreen = false;
    int m_displayMode = 0;
    std::string m_locale = "en";
    bool m_replaying = false;
    float m_skipSpeed = 5.0f;
    bool m_skipEffects = true;
    float m_skipTimer = 0.0f;

    float m_splashTimer = 0.0f;
    float m_splashDuration = 2.8f;
    float m_splashFade = 0.0f;
    float m_splashProgress = 0.0f;

    enum class MainMenuScreen { MAIN, SAVES, SETTINGS, CREDITS };
    MainMenuScreen m_mainMenuScreen = MainMenuScreen::MAIN;
    int m_mainMenuHover = -1;
    float m_mainMenuFade = 0.0f;
    float m_mainMenuTitleY = -30.0f;
    float m_mainMenuBtnAnim[8] = {};

    int m_selectedSaveSlot = -1;

    bool m_confirmActive = false;

    int m_pendingDisplayMode = 0;
    int m_pendingResIndex = 0;
    std::string m_pendingLocale = "en";
    bool m_pendingSkipEffects = true;
    bool m_settingsChanged = false;

    std::unique_ptr<TimeManager> m_timeManager;
    std::unique_ptr<asset::AssetManager> m_assetManager;
    std::unique_ptr<audio::AudioMixer> m_audioMixer;
    std::unique_ptr<render::Renderer> m_renderer;
    std::unique_ptr<render::TweenManager> m_tweenManager;
    std::unique_ptr<text::RichTextParser> m_richText;
    std::unique_ptr<i18n::LocalizationManager> m_localization;
    std::unique_ptr<GameStateManager> m_stateManager;
    std::unique_ptr<script::ScriptVM> m_scriptVM;
    std::unique_ptr<render::ParticleSystem> m_particles;
    std::unique_ptr<SettingsManager> m_settings;
    std::unique_ptr<PostEffects> m_postEffects;

    bool m_saveLocked = false;
    bool m_quickbarVisible = true;
    double m_glitchTextTimer = 0.0;
    double m_glitchUITimer = 0.0;
    double m_windowShakeTimer = 0.0;
    int m_windowOrigX = 0;
    int m_windowOrigY = 0;

    std::string m_textInputBuffer;
    bool m_textInputActive = false;
    float m_cursorBlink = 0.0f;
    float m_inputSlideAnim = 0.0f;
};
} // namespace velora::core
