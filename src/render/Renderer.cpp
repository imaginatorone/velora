
#include "Renderer.h"
#include "asset/AssetManager.h"
#include <SDL3_image/SDL_image.h>
#include <iostream>
#include <algorithm>
#include <cmath>
#include <functional>

namespace velora::render {

Renderer::Renderer(SDL_Renderer* renderer, velora::asset::AssetManager* assets)
    : m_renderer(renderer), m_assets(assets) {}

Renderer::~Renderer() {
    flushTextureCache();
}

void Renderer::flushTextureCache() {
    for (auto& [path, tex] : m_textureCache) {
        if (tex)
            SDL_DestroyTexture(tex);
    }
    m_textureCache.clear();
    m_bgTexture = nullptr;
    m_bgOldTexture = nullptr;
    std::cout << "[Renderer] Texture cache cleared.\n";
}

std::optional<float> Renderer::getSpriteProperty(const std::string& id,
                                                 const std::string& property) const {
    const auto it = std::find_if(m_sprites.begin(), m_sprites.end(),
                                 [&](const SpriteInstance& sprite) { return sprite.id == id; });
    if (it == m_sprites.end())
        return std::nullopt;
    if (property == "x")
        return it->x;
    if (property == "y")
        return it->y;
    if (property == "alpha")
        return it->alpha;
    if (property == "scale" || property == "zoom")
        return it->scaleX;
    if (property == "rotation" || property == "rotate")
        return it->rotation;
    return std::nullopt;
}

bool Renderer::setSpriteProperty(const std::string& id, const std::string& property, float value) {
    const auto it = std::find_if(m_sprites.begin(), m_sprites.end(),
                                 [&](const SpriteInstance& sprite) { return sprite.id == id; });
    if (it == m_sprites.end())
        return false;
    if (property == "x")
        it->x = value;
    else if (property == "y")
        it->y = value;
    else if (property == "alpha")
        it->alpha = std::clamp(value, 0.0f, 1.0f);
    else if (property == "scale" || property == "zoom") {
        it->scaleX = value;
        it->scaleY = value;
    } else if (property == "rotation" || property == "rotate") {
        it->rotation = value;
    } else {
        return false;
    }
    return true;
}

SDL_Texture* Renderer::loadTexture(const std::string& path) {
    auto it = m_textureCache.find(path);
    if (it != m_textureCache.end())
        return it->second;

    std::string fullPath = path;
    if (m_assets)
        fullPath = m_assets->resolvePath(path);

    SDL_Texture* tex = fullPath.empty() ? nullptr : IMG_LoadTexture(m_renderer, fullPath.c_str());
    if (!tex) {
        std::cerr << "[Renderer] Asset unavailable: " << path << "\n";
        m_textureCache[path] = nullptr;
        return nullptr;
    }

    SDL_SetTextureBlendMode(tex, SDL_BLENDMODE_BLEND);
    m_textureCache[path] = tex;
    return tex;
}

void Renderer::setBackground(const std::string& imagePath,
                             const std::optional<script::TransitionSpec>& transition) {
    m_bgOldPath = m_bgPath;
    m_bgOldTexture = m_bgTexture;
    m_bgPath = imagePath;
    m_bgTexture = loadTexture(imagePath);

    if (transition) {
        m_bgTransDuration = static_cast<float>(transition->duration);
        m_bgTransProgress = 0.0f;
    } else {
        m_bgTransProgress = 1.0f;
        m_bgOldTexture = nullptr;
    }
}

void Renderer::showSprite(const std::string& id, const std::string& imagePath,
                          const std::optional<script::PositionSpec>& pos,
                          const std::optional<script::TransitionSpec>& trans, bool flipped,
                          int zorder, float zoom, float rotate) {
    auto it = std::find_if(m_sprites.begin(), m_sprites.end(),
                           [&id](const SpriteInstance& s) { return s.id == id; });

    SpriteInstance sprite;
    sprite.id = id;
    sprite.imagePath = imagePath;
    sprite.x = pos ? pos->x : 0.5f;
    sprite.y = pos ? pos->y : 0.5f;
    sprite.flipped = flipped;
    sprite.zOrder = zorder;
    sprite.scaleX = zoom;
    sprite.scaleY = zoom;
    sprite.rotation = rotate;

    if (trans) {
        sprite.alpha = 0.0f;
        sprite.targetAlpha = 1.0f;
        sprite.transitioning = true;
        sprite.transDuration = static_cast<float>(trans->duration);
        sprite.transProgress = 0.0f;
    }

    loadTexture(imagePath);

    if (it != m_sprites.end()) {
        *it = sprite;
    } else {
        m_sprites.push_back(sprite);
    }

    std::sort(m_sprites.begin(), m_sprites.end(),
              [](const SpriteInstance& a, const SpriteInstance& b) { return a.zOrder < b.zOrder; });
}

void Renderer::hideSprite(const std::string& id,
                          const std::optional<script::TransitionSpec>& trans) {
    auto it = std::find_if(m_sprites.begin(), m_sprites.end(),
                           [&id](const SpriteInstance& s) { return s.id == id; });
    if (it == m_sprites.end())
        return;

    if (trans) {
        it->targetAlpha = 0.0f;
        it->transitioning = true;
        it->transDuration = static_cast<float>(trans->duration);
        it->transProgress = 0.0f;
    } else {
        m_sprites.erase(it);
    }
}

void Renderer::renderBackground(double alpha) {
    (void)alpha;
    if (m_bgOldTexture && m_bgTransProgress < 1.0f) {
        uint8_t oldAlpha = static_cast<uint8_t>((1.0f - m_bgTransProgress) * 255.0f);
        SDL_SetTextureAlphaMod(m_bgOldTexture, oldAlpha);
        SDL_RenderTexture(m_renderer, m_bgOldTexture, nullptr, nullptr);
    }

    if (m_bgTexture) {
        uint8_t curAlpha = static_cast<uint8_t>(m_bgTransProgress * 255.0f);
        SDL_SetTextureAlphaMod(m_bgTexture, curAlpha);
        SDL_RenderTexture(m_renderer, m_bgTexture, nullptr, nullptr);
        SDL_SetTextureAlphaMod(m_bgTexture, 255);
    } else {
        renderProceduralBackground(
            m_bgPath, static_cast<uint8_t>(std::clamp(m_bgTransProgress, 0.0f, 1.0f) * 255.0f));
    }
}

void Renderer::renderSprites(double alpha) {
    (void)alpha;
    for (auto it = m_sprites.begin(); it != m_sprites.end();) {
        auto& sp = *it;
        SDL_Texture* tex = loadTexture(sp.imagePath);
        if (tex) {
            float texW, texH;
            SDL_GetTextureSize(tex, &texW, &texH);

            int winW = 1280, winH = 720;
            SDL_GetRenderLogicalPresentation(m_renderer, &winW, &winH, nullptr);
            float screenW = static_cast<float>(winW);
            float screenH = static_cast<float>(winH);

            float targetH = screenH * 0.8f;
            float scale = targetH / texH;
            float drawW = texW * scale * sp.scaleX;
            float drawH = texH * scale * sp.scaleY;

            float drawX = (sp.x * screenW) - (drawW * 0.5f) + m_shakeOffsetX;
            float drawY = screenH - drawH + m_shakeOffsetY;

            SDL_FRect dst = {drawX, drawY, drawW, drawH};
            SDL_SetTextureAlphaMod(tex, static_cast<uint8_t>(sp.alpha * 255.0f));

            SDL_FlipMode flip = sp.flipped ? SDL_FLIP_HORIZONTAL : SDL_FLIP_NONE;
            if (sp.rotation != 0.0f || sp.flipped) {
                SDL_FPoint center = {drawW * 0.5f, drawH * 0.5f};
                SDL_RenderTextureRotated(m_renderer, tex, nullptr, &dst,
                                         static_cast<double>(sp.rotation), &center, flip);
            } else {
                SDL_RenderTexture(m_renderer, tex, nullptr, &dst);
            }
            SDL_SetTextureAlphaMod(tex, 255);
        }
        ++it;
    }
}

void Renderer::renderEffects(double alpha) {
    (void)alpha;
    if (m_flashTimer > 0.0f && m_flashDuration > 0.0f) {
        const float progress = std::clamp(m_flashTimer / m_flashDuration, 0.0f, 1.0f);
        SDL_SetRenderDrawBlendMode(m_renderer, SDL_BLENDMODE_BLEND);
        SDL_SetRenderDrawColor(m_renderer, m_flashR, m_flashG, m_flashB,
                               static_cast<uint8_t>(progress * 220.0f));
        SDL_FRect full = {0.0f, 0.0f, 1280.0f, 720.0f};
        SDL_RenderFillRect(m_renderer, &full);
    }
}

void Renderer::update(double deltaTime) {
    const float dt = static_cast<float>(std::max(0.0, deltaTime));
    if (m_bgTransProgress < 1.0f) {
        if (m_bgTransDuration <= 0.0f)
            m_bgTransProgress = 1.0f;
        else
            m_bgTransProgress = std::min(1.0f, m_bgTransProgress + dt / m_bgTransDuration);
    }

    for (auto it = m_sprites.begin(); it != m_sprites.end();) {
        auto& sprite = *it;
        if (sprite.transitioning) {
            const float duration = std::max(sprite.transDuration, 0.001f);
            sprite.transProgress = std::min(1.0f, sprite.transProgress + dt / duration);
            const float eased = 1.0f - std::pow(1.0f - sprite.transProgress, 3.0f);
            sprite.alpha += (sprite.targetAlpha - sprite.alpha) * eased;
            if (sprite.transProgress >= 1.0f) {
                sprite.alpha = sprite.targetAlpha;
                sprite.transitioning = false;
            }
        }
        if (!sprite.transitioning && sprite.alpha <= 0.0f)
            it = m_sprites.erase(it);
        else
            ++it;
    }

    if (m_shakeTimer > 0.0f && m_shakeDuration > 0.0f) {
        m_shakeTimer = std::max(0.0f, m_shakeTimer - dt);
        const float intensity = m_shakeIntensity * (m_shakeTimer / m_shakeDuration);
        std::uniform_real_distribution<float> offset(-intensity, intensity);
        m_shakeOffsetX = offset(m_rng);
        m_shakeOffsetY = offset(m_rng);
    } else {
        m_shakeOffsetX = m_shakeOffsetY = 0.0f;
    }
    m_flashTimer = std::max(0.0f, m_flashTimer - dt);
}

void Renderer::shakeScreen(float intensity, float duration) {
    m_shakeIntensity = intensity;
    m_shakeDuration = duration;
    m_shakeTimer = duration;
}

void Renderer::flashScreen(uint8_t r, uint8_t g, uint8_t b, float duration) {
    m_flashR = r;
    m_flashG = g;
    m_flashB = b;
    m_flashDuration = std::max(duration, 0.0f);
    m_flashTimer = m_flashDuration;
}

void Renderer::clearAllSprites(const std::optional<script::TransitionSpec>& trans) {
    if (trans) {
        for (auto& sp : m_sprites) {
            sp.targetAlpha = 0.0f;
            sp.transitioning = true;
            sp.transDuration = static_cast<float>(trans->duration);
            sp.transProgress = 0.0f;
        }
    } else {
        m_sprites.clear();
    }
}

void Renderer::renderProceduralBackground(const std::string& seedPath, uint8_t opacity) {
    const size_t seed = std::hash<std::string>{}(seedPath);
    const uint8_t accentR = static_cast<uint8_t>(42 + seed % 24);
    const uint8_t accentG = static_cast<uint8_t>(58 + (seed >> 8) % 30);
    const uint8_t accentB = static_cast<uint8_t>(64 + (seed >> 16) % 28);
    constexpr int bands = 24;
    for (int band = 0; band < bands; ++band) {
        const float t = static_cast<float>(band) / static_cast<float>(bands - 1);
        SDL_SetRenderDrawColor(m_renderer, static_cast<uint8_t>(10 + accentR * t * 0.55f),
                               static_cast<uint8_t>(14 + accentG * t * 0.48f),
                               static_cast<uint8_t>(18 + accentB * t * 0.42f), opacity);
        SDL_FRect rect = {0.0f, band * (720.0f / bands), 1280.0f, 720.0f / bands + 1.0f};
        SDL_RenderFillRect(m_renderer, &rect);
    }
    SDL_SetRenderDrawColor(m_renderer, 172, 204, 157, static_cast<uint8_t>(opacity / 10));
    for (float x = 0.0f; x < 1280.0f; x += 80.0f)
        SDL_RenderLine(m_renderer, x, 0.0f, x, 720.0f);
    for (float y = 0.0f; y < 720.0f; y += 80.0f)
        SDL_RenderLine(m_renderer, 0.0f, y, 1280.0f, y);
}

} // namespace velora::render
