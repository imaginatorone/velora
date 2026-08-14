
#pragma once

#include <SDL3/SDL.h>
#include <string>
#include <vector>
#include <cstdint>

namespace velora::core {

class SettingsManager;

enum class PostEffectType {
    BLUR,
    VIGNETTE,
    CRT,
    GLITCH,
    COLOR_GRADE,
    FADE,
};

struct PostEffect {
    PostEffectType type;
    float intensity = 1.0f;
    float duration = 0.0f;
    float elapsed = 0.0f;
    bool active = true;

    float r = 0, g = 0, b = 0;
};

class PostEffects {
  public:
    PostEffects();
    ~PostEffects();

    bool initialize(SDL_Renderer* renderer, int width, int height);

    void cleanup();

    void setEffect(PostEffectType type, float intensity, float duration = 0.0f);

    void setEffectColor(PostEffectType type, float r, float g, float b);

    void removeEffect(PostEffectType type);

    void clearAll();

    [[nodiscard]] bool hasActiveEffects() const;

    void beginCapture(SDL_Renderer* renderer);

    void endCapture(SDL_Renderer* renderer, const SettingsManager* settings);

    void update(double dt);

  private:
    SDL_Texture* m_sceneTarget = nullptr;
    SDL_Texture* m_effectTarget = nullptr;
    int m_width = 1280;
    int m_height = 720;
    bool m_initialized = false;
    bool m_capturing = false;

    std::vector<PostEffect> m_effects;

    void applyVignette(SDL_Renderer* renderer, float intensity);

    void applyCRT(SDL_Renderer* renderer, float intensity);

    void applyGlitch(SDL_Renderer* renderer, float intensity);

    void applyFade(SDL_Renderer* renderer, float intensity, float r, float g, float b);

    void applyColorGrade(SDL_Renderer* renderer, float r, float g, float b, float intensity);

    void applyBlur(SDL_Renderer* renderer, float intensity);

    PostEffect* findEffect(PostEffectType type);
};

} // namespace velora::core
