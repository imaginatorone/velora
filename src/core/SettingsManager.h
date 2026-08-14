#pragma once

#include <SDL3/SDL.h>

#include <filesystem>
#include <string>

namespace velora::core {

struct EngineConfig;

enum class QualityPreset { LOW, MEDIUM, HIGH, ULTRA, CUSTOM };

class SettingsManager {
  public:
    explicit SettingsManager(std::filesystem::path filePath = "settings.json");

    float textSpeed = 30.0f;
    float autoDelay = 1.5f;
    float musicVolume = 0.8f;
    float soundVolume = 0.8f;
    bool isFullscreen = false;
    int displayMode = 0;
    std::string locale = "en";
    float skipSpeed = 5.0f;
    bool skipEffects = true;
    int windowWidth = 1280;
    int windowHeight = 720;

    QualityPreset qualityPreset = QualityPreset::HIGH;
    bool enableBlur = true;
    bool enableParticles = true;
    bool enablePostFX = true;
    bool enableAnimations = true;
    int particleDensity = 100;
    int targetFPS = 60;

    int pendingDisplayMode = 0;
    int pendingResIndex = 0;
    std::string pendingLocale = "en";
    bool pendingSkipEffects = true;
    bool settingsChanged = false;

    void save();
    void load();
    void apply(SDL_Window* window, SDL_Renderer* renderer, EngineConfig& config);
    void resetPending(const EngineConfig& config);
    [[nodiscard]] bool hasChanges(const EngineConfig& config) const;

    void autoDetectQuality();
    void applyQualityPreset(QualityPreset preset);

    static constexpr int RES_COUNT = 4;
    static constexpr int RES_W[RES_COUNT] = {1280, 1366, 1600, 1920};
    static constexpr int RES_H[RES_COUNT] = {720, 768, 900, 1080};
    [[nodiscard]] int findResIndex(int width, int height) const;

  private:
    void sanitize();

    std::filesystem::path m_filePath;
};

} // namespace velora::core
