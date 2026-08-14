#include "SettingsManager.h"
#include "Engine.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <system_error>

namespace fs = std::filesystem;

namespace velora::core {

SettingsManager::SettingsManager(fs::path filePath) : m_filePath(std::move(filePath)) {}

void SettingsManager::save() {
    try {
        sanitize();
        nlohmann::json data = {
            {"display_mode", displayMode},
            {"locale", locale},
            {"music_volume", musicVolume},
            {"sound_volume", soundVolume},
            {"text_speed", textSpeed},
            {"auto_delay", autoDelay},
            {"skip_speed", skipSpeed},
            {"skip_effects", skipEffects},
            {"fullscreen", isFullscreen},
            {"window_width", windowWidth},
            {"window_height", windowHeight},
            {"quality_preset", static_cast<int>(qualityPreset)},
            {"enable_blur", enableBlur},
            {"enable_particles", enableParticles},
            {"enable_postfx", enablePostFX},
            {"enable_animations", enableAnimations},
            {"particle_density", particleDensity},
            {"target_fps", targetFPS},
        };

        std::error_code error;
        if (!m_filePath.parent_path().empty()) {
            fs::create_directories(m_filePath.parent_path(), error);
            if (error)
                throw std::runtime_error(error.message());
        }

        const fs::path temporaryPath = m_filePath.string() + ".tmp";
        {
            std::ofstream output(temporaryPath, std::ios::trunc);
            if (!output)
                throw std::runtime_error("cannot open temporary settings file");
            output << data.dump(2) << '\n';
            if (!output)
                throw std::runtime_error("cannot write settings file");
        }

        fs::remove(m_filePath, error);
        error.clear();
        fs::rename(temporaryPath, m_filePath, error);
        if (error)
            throw std::runtime_error(error.message());
    } catch (const std::exception& error) {
        std::cerr << "[Settings] Could not save settings: " << error.what() << '\n';
    }
}

void SettingsManager::load() {
    try {
        std::ifstream input(m_filePath);
        if (!input)
            return;

        const nlohmann::json data = nlohmann::json::parse(input);
        displayMode = data.value("display_mode", displayMode);
        locale = data.value("locale", locale);
        musicVolume = data.value("music_volume", musicVolume);
        soundVolume = data.value("sound_volume", soundVolume);
        textSpeed = data.value("text_speed", textSpeed);
        autoDelay = data.value("auto_delay", autoDelay);
        skipSpeed = data.value("skip_speed", skipSpeed);
        skipEffects = data.value("skip_effects", skipEffects);
        isFullscreen = data.value("fullscreen", isFullscreen);
        windowWidth = data.value("window_width", windowWidth);
        windowHeight = data.value("window_height", windowHeight);
        qualityPreset = static_cast<QualityPreset>(
            data.value("quality_preset", static_cast<int>(qualityPreset)));
        enableBlur = data.value("enable_blur", enableBlur);
        enableParticles = data.value("enable_particles", enableParticles);
        enablePostFX = data.value("enable_postfx", enablePostFX);
        enableAnimations = data.value("enable_animations", enableAnimations);
        particleDensity = data.value("particle_density", particleDensity);
        targetFPS = data.value("target_fps", targetFPS);
        sanitize();
    } catch (const std::exception& error) {
        std::cerr << "[Settings] Ignoring invalid settings file: " << error.what() << '\n';
    }
}

void SettingsManager::apply(SDL_Window* window, SDL_Renderer* renderer, EngineConfig& config) {
    pendingDisplayMode = std::clamp(pendingDisplayMode, 0, 2);
    pendingResIndex = std::clamp(pendingResIndex, 0, RES_COUNT - 1);
    displayMode = pendingDisplayMode;

    if (displayMode == 0) {
        SDL_SetWindowFullscreen(window, false);
        SDL_SetWindowBordered(window, true);
        isFullscreen = false;
    } else if (displayMode == 1) {
        SDL_SetWindowFullscreen(window, false);
        SDL_SetWindowBordered(window, false);
        if (const SDL_DisplayMode* mode = SDL_GetDesktopDisplayMode(SDL_GetPrimaryDisplay())) {
            SDL_SetWindowSize(window, mode->w, mode->h);
        }
        SDL_SetWindowPosition(window, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
        isFullscreen = false;
    } else {
        SDL_SetWindowFullscreen(window, true);
        isFullscreen = true;
    }

    config.windowWidth = RES_W[pendingResIndex];
    config.windowHeight = RES_H[pendingResIndex];
    windowWidth = config.windowWidth;
    windowHeight = config.windowHeight;
    if (displayMode == 0) {
        SDL_SetWindowSize(window, windowWidth, windowHeight);
        SDL_SetWindowPosition(window, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
    }
    SDL_SetRenderLogicalPresentation(renderer, 1280, 720, SDL_LOGICAL_PRESENTATION_LETTERBOX);

    locale = pendingLocale;
    skipEffects = pendingSkipEffects;
    settingsChanged = false;
    save();
}

void SettingsManager::resetPending(const EngineConfig& config) {
    pendingDisplayMode = displayMode;
    pendingLocale = locale;
    pendingSkipEffects = skipEffects;
    pendingResIndex = findResIndex(config.windowWidth, config.windowHeight);
    settingsChanged = false;
}

bool SettingsManager::hasChanges(const EngineConfig& config) const {
    return pendingDisplayMode != displayMode ||
           pendingResIndex != findResIndex(config.windowWidth, config.windowHeight) ||
           pendingLocale != locale || pendingSkipEffects != skipEffects;
}

void SettingsManager::autoDetectQuality() {
    const int ram = SDL_GetSystemRAM();
    if (ram >= 8192)
        applyQualityPreset(QualityPreset::ULTRA);
    else if (ram >= 4096)
        applyQualityPreset(QualityPreset::HIGH);
    else if (ram >= 2048)
        applyQualityPreset(QualityPreset::MEDIUM);
    else
        applyQualityPreset(QualityPreset::LOW);
}

void SettingsManager::applyQualityPreset(QualityPreset preset) {
    qualityPreset = preset;
    switch (preset) {
    case QualityPreset::LOW:
        enableBlur = false;
        enableParticles = false;
        enablePostFX = false;
        enableAnimations = false;
        particleDensity = 30;
        targetFPS = 30;
        break;
    case QualityPreset::MEDIUM:
        enableBlur = false;
        enableParticles = true;
        enablePostFX = false;
        enableAnimations = true;
        particleDensity = 60;
        targetFPS = 60;
        break;
    case QualityPreset::HIGH:
        enableBlur = true;
        enableParticles = true;
        enablePostFX = true;
        enableAnimations = true;
        particleDensity = 100;
        targetFPS = 60;
        break;
    case QualityPreset::ULTRA:
        enableBlur = true;
        enableParticles = true;
        enablePostFX = true;
        enableAnimations = true;
        particleDensity = 200;
        targetFPS = 120;
        break;
    case QualityPreset::CUSTOM:
        break;
    }
}

int SettingsManager::findResIndex(int width, int height) const {
    for (int index = 0; index < RES_COUNT; ++index) {
        if (width == RES_W[index] && height == RES_H[index])
            return index;
    }
    return 0;
}

void SettingsManager::sanitize() {
    displayMode = std::clamp(displayMode, 0, 2);
    textSpeed = std::clamp(textSpeed, 5.0f, 120.0f);
    autoDelay = std::clamp(autoDelay, 0.2f, 10.0f);
    musicVolume = std::clamp(musicVolume, 0.0f, 1.0f);
    soundVolume = std::clamp(soundVolume, 0.0f, 1.0f);
    skipSpeed = std::clamp(skipSpeed, 1.0f, 30.0f);
    particleDensity = std::clamp(particleDensity, 10, 200);
    targetFPS = std::clamp(targetFPS, 30, 240);
    locale = "en";
    const int resolution = findResIndex(windowWidth, windowHeight);
    windowWidth = RES_W[resolution];
    windowHeight = RES_H[resolution];
    const int preset = std::clamp(static_cast<int>(qualityPreset), 0, 4);
    qualityPreset = static_cast<QualityPreset>(preset);
}

} // namespace velora::core
