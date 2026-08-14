
#pragma once

#include "script/Parser.h"
#include <SDL3/SDL.h>
#include <string>
#include <vector>
#include <unordered_map>
#include <optional>
#include <random>

namespace velora::asset {
class AssetManager;
}

namespace velora::render {

struct SpriteInstance {
    std::string id;
    std::string imagePath;
    float x = 0.5f, y = 0.5f;
    float alpha = 1.0f;
    float scaleX = 1.0f, scaleY = 1.0f;
    float rotation = 0.0f;
    int zOrder = 0;
    bool flipped = false;
    bool transitioning = false;
    float transProgress = 0.0f;
    float transDuration = 0.0f;
    float targetAlpha = 1.0f;
};

class Renderer {
  public:
    Renderer(SDL_Renderer* renderer, velora::asset::AssetManager* assets);
    ~Renderer();

    void setBackground(const std::string& imagePath,
                       const std::optional<script::TransitionSpec>& transition = std::nullopt);

    void showSprite(const std::string& id, const std::string& imagePath,
                    const std::optional<script::PositionSpec>& pos = std::nullopt,
                    const std::optional<script::TransitionSpec>& trans = std::nullopt,
                    bool flipped = false, int zorder = 0, float zoom = 1.0f, float rotate = 0.0f);
    void hideSprite(const std::string& id,
                    const std::optional<script::TransitionSpec>& trans = std::nullopt);
    void clearAllSprites(const std::optional<script::TransitionSpec>& trans = std::nullopt);

    void update(double deltaTime);
    void renderBackground(double alpha);
    void renderSprites(double alpha);
    void renderEffects(double alpha);

    void shakeScreen(float intensity, float duration);
    void flashScreen(uint8_t r, uint8_t g, uint8_t b, float duration);

    SDL_Texture* loadTexture(const std::string& path);

    void flushTextureCache();

    [[nodiscard]] const std::string& getCurrentBackground() const { return m_bgPath; }
    [[nodiscard]] const std::vector<SpriteInstance>& getSprites() const { return m_sprites; }
    [[nodiscard]] std::optional<float> getSpriteProperty(const std::string& id,
                                                         const std::string& property) const;
    bool setSpriteProperty(const std::string& id, const std::string& property, float value);

  private:
    SDL_Renderer* m_renderer;
    velora::asset::AssetManager* m_assets;

    std::string m_bgPath;
    SDL_Texture* m_bgTexture = nullptr;
    std::string m_bgOldPath;
    SDL_Texture* m_bgOldTexture = nullptr;
    float m_bgTransProgress = 1.0f;
    float m_bgTransDuration = 0.0f;

    std::vector<SpriteInstance> m_sprites;

    std::unordered_map<std::string, SDL_Texture*> m_textureCache;

    float m_shakeIntensity = 0.0f;
    float m_shakeDuration = 0.0f;
    float m_shakeTimer = 0.0f;
    float m_shakeOffsetX = 0.0f;
    float m_shakeOffsetY = 0.0f;

    uint8_t m_flashR = 255, m_flashG = 255, m_flashB = 255;
    float m_flashDuration = 0.0f;
    float m_flashTimer = 0.0f;
    std::mt19937 m_rng{std::random_device{}()};

    void renderProceduralBackground(const std::string& seedPath, uint8_t opacity);
};

} // namespace velora::render
