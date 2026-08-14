
#pragma once

#include <SDL3/SDL.h>
#include <string>
#include <vector>
#include <random>

namespace velora::render {

enum class ParticlePreset { RAIN, SNOW, FIRE, SPARKLE, DUST, PETALS, CUSTOM };

struct Particle {
    float x, y;
    float vx, vy;
    float life, maxLife;
    float size;
    float rotation;
    float rotSpeed;
    SDL_Color color;
    float alpha;
};

struct ParticleEmitter {
    std::string name;
    ParticlePreset preset = ParticlePreset::RAIN;
    bool active = true;

    float spawnRate = 100.0f;
    float spawnAccum = 0.0f;

    float areaX = 0.0f, areaY = 0.0f;
    float areaW = 1.0f, areaH = 0.0f;

    float gravity = 200.0f;
    float wind = 0.0f;

    float speedMin = 100, speedMax = 300;
    float angleMin = 85, angleMax = 95;
    float sizeMin = 1, sizeMax = 3;
    float lifeMin = 1.0f, lifeMax = 3.0f;
    float rotSpeedMin = 0, rotSpeedMax = 0;

    SDL_Color colorStart = {200, 220, 255, 200};
    SDL_Color colorEnd = {150, 180, 220, 0};

    float intensity = 1.0f;

    size_t maxParticles = 2000;
};

class ParticleSystem {
  public:
    ParticleSystem();
    ~ParticleSystem();

    int addEmitter(const ParticleEmitter& emitter);

    int startPreset(ParticlePreset preset, float intensity = 1.0f, float wind = 0.0f);

    void stopEmitter(const std::string& name);

    void stopAll();

    void update(double dt, float screenW, float screenH);

    void render(SDL_Renderer* renderer);

    [[nodiscard]] bool hasActiveEmitters() const;

    static ParticleEmitter makePreset(ParticlePreset preset, float intensity, float wind);

  private:
    void spawnParticle(ParticleEmitter& emitter, float screenW, float screenH);

    std::vector<ParticleEmitter> m_emitters;
    std::vector<std::vector<Particle>> m_particles;
    std::mt19937 m_rng;

    float randFloat(float min, float max);
    uint8_t lerpByte(uint8_t a, uint8_t b, float t);
};

} // namespace velora::render
