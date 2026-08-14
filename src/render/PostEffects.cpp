
#include "PostEffects.h"
#include "../core/SettingsManager.h"

#include <SDL3/SDL.h>
#include <iostream>
#include <cmath>
#include <cstdlib>
#include <algorithm>

namespace velora::core {

PostEffects::PostEffects() = default;

PostEffects::~PostEffects() {
    cleanup();
}

bool PostEffects::initialize(SDL_Renderer* renderer, int width, int height) {
    m_width = width;
    m_height = height;

    m_sceneTarget = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_RGBA8888, SDL_TEXTUREACCESS_TARGET,
                                      width, height);
    if (!m_sceneTarget) {
        std::cerr << "[PostFX] Failed to create scene target: " << SDL_GetError() << "\n";
        return false;
    }
    SDL_SetTextureBlendMode(m_sceneTarget, SDL_BLENDMODE_BLEND);

    int halfW = std::max(width / 4, 64);
    int halfH = std::max(height / 4, 36);
    m_effectTarget = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_RGBA8888, SDL_TEXTUREACCESS_TARGET,
                                       halfW, halfH);
    if (!m_effectTarget) {
        std::cerr << "[PostFX] Failed to create effect target: " << SDL_GetError() << "\n";
        return false;
    }
    SDL_SetTextureBlendMode(m_effectTarget, SDL_BLENDMODE_BLEND);

    m_initialized = true;
    std::cout << "[PostFX] Initialized (" << width << "x" << height << ", blur buffer " << halfW
              << "x" << halfH << ")\n";
    return true;
}

void PostEffects::cleanup() {
    if (m_sceneTarget) {
        SDL_DestroyTexture(m_sceneTarget);
        m_sceneTarget = nullptr;
    }
    if (m_effectTarget) {
        SDL_DestroyTexture(m_effectTarget);
        m_effectTarget = nullptr;
    }
    m_initialized = false;
}

void PostEffects::setEffect(PostEffectType type, float intensity, float duration) {
    auto* existing = findEffect(type);
    if (existing) {
        existing->intensity = intensity;
        existing->duration = duration;
        existing->elapsed = 0.0f;
        existing->active = true;
    } else {
        PostEffect fx;
        fx.type = type;
        fx.intensity = intensity;
        fx.duration = duration;
        fx.elapsed = 0.0f;
        fx.active = true;
        m_effects.push_back(fx);
    }
    std::cout << "[PostFX] Set effect type=" << static_cast<int>(type) << " intensity=" << intensity
              << " duration=" << duration << "\n";
}

void PostEffects::setEffectColor(PostEffectType type, float r, float g, float b) {
    auto* fx = findEffect(type);
    if (fx) {
        fx->r = r;
        fx->g = g;
        fx->b = b;
    }
}

void PostEffects::removeEffect(PostEffectType type) {
    m_effects.erase(std::remove_if(m_effects.begin(), m_effects.end(),
                                   [type](const PostEffect& fx) { return fx.type == type; }),
                    m_effects.end());
}

void PostEffects::clearAll() {
    m_effects.clear();
    std::cout << "[PostFX] All effects cleared\n";
}

bool PostEffects::hasActiveEffects() const {
    return !m_effects.empty();
}

PostEffect* PostEffects::findEffect(PostEffectType type) {
    for (auto& fx : m_effects) {
        if (fx.type == type)
            return &fx;
    }
    return nullptr;
}

void PostEffects::update(double dt) {
    for (auto& fx : m_effects) {
        if (fx.duration > 0.0f) {
            fx.elapsed += static_cast<float>(dt);
            if (fx.elapsed >= fx.duration) {
                fx.active = false;
            }
        }
    }
    m_effects.erase(std::remove_if(m_effects.begin(), m_effects.end(),
                                   [](const PostEffect& fx) { return !fx.active; }),
                    m_effects.end());
}

void PostEffects::beginCapture(SDL_Renderer* renderer) {
    if (!m_initialized || m_effects.empty()) {
        m_capturing = false;
        return;
    }

    bool needsCapture = false;
    for (auto& fx : m_effects) {
        if (fx.type == PostEffectType::BLUR) {
            needsCapture = true;
            break;
        }
    }

    if (needsCapture && m_sceneTarget) {
        SDL_SetRenderTarget(renderer, m_sceneTarget);
        SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
        SDL_RenderClear(renderer);
        m_capturing = true;
    } else {
        m_capturing = false;
    }
}

void PostEffects::endCapture(SDL_Renderer* renderer, const SettingsManager* settings) {
    if (!m_initialized)
        return;

    bool postFXEnabled = true;
    if (settings) {
        postFXEnabled = settings->enablePostFX;
    }
    if (!postFXEnabled) {
        if (m_capturing) {
            SDL_SetRenderTarget(renderer, nullptr);
            SDL_RenderTexture(renderer, m_sceneTarget, nullptr, nullptr);
            m_capturing = false;
        }
        return;
    }

    if (m_capturing && m_sceneTarget) {
        SDL_SetRenderTarget(renderer, nullptr);

        auto* blurFx = findEffect(PostEffectType::BLUR);
        if (blurFx && blurFx->active) {
            applyBlur(renderer, blurFx->intensity);
        } else {
            SDL_RenderTexture(renderer, m_sceneTarget, nullptr, nullptr);
        }
        m_capturing = false;
    }

    SDL_SetRenderDrawBlendMode(renderer, SDL_BLENDMODE_BLEND);

    for (auto& fx : m_effects) {
        if (!fx.active)
            continue;

        float effectIntensity = fx.intensity;
        if (fx.duration > 0.0f) {
            float remaining = fx.duration - fx.elapsed;
            if (remaining < 0.3f) {
                effectIntensity *= remaining / 0.3f;
            }
        }

        switch (fx.type) {
        case PostEffectType::VIGNETTE:
            applyVignette(renderer, effectIntensity);
            break;
        case PostEffectType::CRT:
            applyCRT(renderer, effectIntensity);
            break;
        case PostEffectType::GLITCH:
            applyGlitch(renderer, effectIntensity);
            break;
        case PostEffectType::FADE:
            applyFade(renderer, effectIntensity, fx.r, fx.g, fx.b);
            break;
        case PostEffectType::COLOR_GRADE:
            applyColorGrade(renderer, fx.r, fx.g, fx.b, effectIntensity);
            break;
        case PostEffectType::BLUR:
            break;
        }
    }
}

void PostEffects::applyVignette(SDL_Renderer* renderer, float intensity) {
    float w = static_cast<float>(m_width);
    float h = static_cast<float>(m_height);

    int steps = 40;
    for (int i = 0; i < steps; i++) {
        float t = static_cast<float>(i) / steps;
        float inset = t * std::min(w, h) * 0.35f;

        float edgeFactor = 1.0f - t;
        edgeFactor = edgeFactor * edgeFactor;
        uint8_t alpha = static_cast<uint8_t>(edgeFactor * intensity * 180.0f);
        if (alpha == 0)
            continue;

        SDL_SetRenderDrawColor(renderer, 0, 0, 0, alpha);

        SDL_FRect top = {inset, inset, w - inset * 2, 1.0f};
        SDL_RenderFillRect(renderer, &top);
        SDL_FRect bottom = {inset, h - inset - 1, w - inset * 2, 1.0f};
        SDL_RenderFillRect(renderer, &bottom);
        SDL_FRect left = {inset, inset, 1.0f, h - inset * 2};
        SDL_RenderFillRect(renderer, &left);
        SDL_FRect right = {w - inset - 1, inset, 1.0f, h - inset * 2};
        SDL_RenderFillRect(renderer, &right);
    }
}

void PostEffects::applyCRT(SDL_Renderer* renderer, float intensity) {
    float w = static_cast<float>(m_width);
    float h = static_cast<float>(m_height);
    uint8_t alpha = static_cast<uint8_t>(intensity * 60.0f);

    SDL_SetRenderDrawColor(renderer, 0, 0, 0, alpha);
    for (float y = 0; y < h; y += 3.0f) {
        SDL_FRect line = {0, y, w, 1.0f};
        SDL_RenderFillRect(renderer, &line);
    }

    applyVignette(renderer, intensity * 0.3f);

    uint8_t fringeAlpha = static_cast<uint8_t>(intensity * 25.0f);
    SDL_SetRenderDrawColor(renderer, 255, 0, 0, fringeAlpha);
    SDL_FRect leftFringe = {0, 0, 2.0f, h};
    SDL_RenderFillRect(renderer, &leftFringe);
    SDL_SetRenderDrawColor(renderer, 0, 0, 255, fringeAlpha);
    SDL_FRect rightFringe = {w - 2, 0, 2.0f, h};
    SDL_RenderFillRect(renderer, &rightFringe);
}

void PostEffects::applyGlitch(SDL_Renderer* renderer, float intensity) {
    float w = static_cast<float>(m_width);
    float h = static_cast<float>(m_height);

    int stripCount = static_cast<int>(5 + intensity * 15);
    for (int i = 0; i < stripCount; i++) {
        float sy = static_cast<float>(rand() % static_cast<int>(h));
        float sh = 1.0f + static_cast<float>(rand() % 4);
        float offset = (static_cast<float>(rand() % 40) - 20.0f) * intensity;

        int channel = rand() % 3;
        uint8_t r = (channel == 0) ? 255 : 0;
        uint8_t g = (channel == 1) ? 255 : 0;
        uint8_t b = (channel == 2) ? 255 : 0;
        uint8_t a = static_cast<uint8_t>(20 + intensity * 60);

        SDL_SetRenderDrawColor(renderer, r, g, b, a);
        SDL_FRect strip = {offset, sy, w, sh};
        SDL_RenderFillRect(renderer, &strip);
    }

    int blockCount = static_cast<int>(intensity * 3);
    for (int i = 0; i < blockCount; i++) {
        float bx = static_cast<float>(rand() % static_cast<int>(w));
        float by = static_cast<float>(rand() % static_cast<int>(h));
        float bw = 20.0f + static_cast<float>(rand() % 100);
        float bh = 2.0f + static_cast<float>(rand() % 6);
        uint8_t ba = static_cast<uint8_t>(40 + rand() % 60);
        SDL_SetRenderDrawColor(renderer, 255, 255, 255, ba);
        SDL_FRect block = {bx, by, bw, bh};
        SDL_RenderFillRect(renderer, &block);
    }
}

void PostEffects::applyFade(SDL_Renderer* renderer, float intensity, float r, float g, float b) {
    uint8_t alpha = static_cast<uint8_t>(std::clamp(intensity, 0.0f, 1.0f) * 255);
    SDL_SetRenderDrawColor(renderer, static_cast<uint8_t>(r * 255), static_cast<uint8_t>(g * 255),
                           static_cast<uint8_t>(b * 255), alpha);
    SDL_FRect full = {0, 0, static_cast<float>(m_width), static_cast<float>(m_height)};
    SDL_RenderFillRect(renderer, &full);
}

void PostEffects::applyColorGrade(SDL_Renderer* renderer, float r, float g, float b,
                                  float intensity) {
    uint8_t alpha = static_cast<uint8_t>(intensity * 80.0f);
    SDL_SetRenderDrawColor(renderer, static_cast<uint8_t>(r * 255), static_cast<uint8_t>(g * 255),
                           static_cast<uint8_t>(b * 255), alpha);
    SDL_FRect full = {0, 0, static_cast<float>(m_width), static_cast<float>(m_height)};
    SDL_RenderFillRect(renderer, &full);
}

void PostEffects::applyBlur(SDL_Renderer* renderer, float intensity) {
    if (!m_sceneTarget || !m_effectTarget)
        return;

    int passes = std::clamp(static_cast<int>(intensity * 3), 1, 4);

    SDL_Texture* source = m_sceneTarget;

    for (int pass = 0; pass < passes; pass++) {
        SDL_SetRenderTarget(renderer, m_effectTarget);
        SDL_RenderTexture(renderer, source, nullptr, nullptr);

        if (pass < passes - 1) {
            SDL_SetRenderTarget(renderer, m_sceneTarget);
            SDL_RenderTexture(renderer, m_effectTarget, nullptr, nullptr);
            source = m_sceneTarget;
        } else {
            SDL_SetRenderTarget(renderer, nullptr);
            SDL_RenderTexture(renderer, m_effectTarget, nullptr, nullptr);
        }
    }
}

} // namespace velora::core
