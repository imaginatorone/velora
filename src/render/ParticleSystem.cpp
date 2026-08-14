
#include "ParticleSystem.h"
#include <cmath>
#include <algorithm>
#include <iostream>

namespace velora::render {

ParticleSystem::ParticleSystem() : m_rng(std::random_device{}()) {}

ParticleSystem::~ParticleSystem() = default;

float ParticleSystem::randFloat(float min, float max) {
    std::uniform_real_distribution<float> dist(min, max);
    return dist(m_rng);
}

uint8_t ParticleSystem::lerpByte(uint8_t a, uint8_t b, float t) {
    return static_cast<uint8_t>(a + (b - a) * t);
}

ParticleEmitter ParticleSystem::makePreset(ParticlePreset preset, float intensity, float wind) {
    ParticleEmitter e;
    e.preset = preset;
    e.intensity = intensity;
    e.wind = wind;

    switch (preset) {
    case ParticlePreset::RAIN:
        e.name = "rain";
        e.spawnRate = 300 * intensity;
        e.areaX = -0.1f;
        e.areaY = -0.05f;
        e.areaW = 1.2f;
        e.areaH = 0.0f;
        e.gravity = 400.0f;
        e.speedMin = 400;
        e.speedMax = 700;
        e.angleMin = 82 + wind * 10;
        e.angleMax = 88 + wind * 10;
        e.sizeMin = 1.0f;
        e.sizeMax = 2.5f;
        e.lifeMin = 0.8f;
        e.lifeMax = 1.5f;
        e.colorStart = {180, 200, 240, 180};
        e.colorEnd = {120, 150, 200, 0};
        e.maxParticles = 3000;
        break;

    case ParticlePreset::SNOW:
        e.name = "snow";
        e.spawnRate = 80 * intensity;
        e.areaX = -0.1f;
        e.areaY = -0.05f;
        e.areaW = 1.2f;
        e.areaH = 0.0f;
        e.gravity = 20.0f;
        e.wind = wind * 30.0f;
        e.speedMin = 30;
        e.speedMax = 80;
        e.angleMin = 80;
        e.angleMax = 100;
        e.sizeMin = 2.0f;
        e.sizeMax = 6.0f;
        e.lifeMin = 4.0f;
        e.lifeMax = 8.0f;
        e.rotSpeedMin = -60;
        e.rotSpeedMax = 60;
        e.colorStart = {240, 245, 255, 200};
        e.colorEnd = {220, 230, 245, 0};
        e.maxParticles = 1500;
        break;

    case ParticlePreset::FIRE:
        e.name = "fire";
        e.spawnRate = 120 * intensity;
        e.areaX = 0.45f;
        e.areaY = 0.85f;
        e.areaW = 0.1f;
        e.areaH = 0.02f;
        e.gravity = -150.0f;
        e.speedMin = 40;
        e.speedMax = 120;
        e.angleMin = 250;
        e.angleMax = 290;
        e.sizeMin = 3.0f;
        e.sizeMax = 8.0f;
        e.lifeMin = 0.5f;
        e.lifeMax = 1.5f;
        e.colorStart = {255, 180, 50, 220};
        e.colorEnd = {200, 50, 20, 0};
        e.maxParticles = 1000;
        break;

    case ParticlePreset::SPARKLE:
        e.name = "sparkle";
        e.spawnRate = 40 * intensity;
        e.areaX = 0.0f;
        e.areaY = 0.0f;
        e.areaW = 1.0f;
        e.areaH = 1.0f;
        e.gravity = 0.0f;
        e.speedMin = 5;
        e.speedMax = 30;
        e.angleMin = 0;
        e.angleMax = 360;
        e.sizeMin = 1.0f;
        e.sizeMax = 4.0f;
        e.lifeMin = 0.5f;
        e.lifeMax = 2.0f;
        e.rotSpeedMin = -90;
        e.rotSpeedMax = 90;
        e.colorStart = {255, 255, 200, 230};
        e.colorEnd = {255, 220, 150, 0};
        e.maxParticles = 500;
        break;

    case ParticlePreset::DUST:
        e.name = "dust";
        e.spawnRate = 15 * intensity;
        e.areaX = 0.0f;
        e.areaY = 0.2f;
        e.areaW = 1.0f;
        e.areaH = 0.6f;
        e.gravity = 5.0f;
        e.speedMin = 3;
        e.speedMax = 15;
        e.angleMin = 0;
        e.angleMax = 360;
        e.sizeMin = 1.0f;
        e.sizeMax = 3.0f;
        e.lifeMin = 3.0f;
        e.lifeMax = 7.0f;
        e.colorStart = {200, 180, 150, 80};
        e.colorEnd = {180, 160, 130, 0};
        e.maxParticles = 300;
        break;

    case ParticlePreset::PETALS:
        e.name = "petals";
        e.spawnRate = 25 * intensity;
        e.areaX = -0.1f;
        e.areaY = -0.05f;
        e.areaW = 1.2f;
        e.areaH = 0.0f;
        e.gravity = 30.0f;
        e.wind = wind * 40.0f + 20.0f;
        e.speedMin = 20;
        e.speedMax = 60;
        e.angleMin = 70;
        e.angleMax = 110;
        e.sizeMin = 4.0f;
        e.sizeMax = 10.0f;
        e.lifeMin = 4.0f;
        e.lifeMax = 8.0f;
        e.rotSpeedMin = -120;
        e.rotSpeedMax = 120;
        e.colorStart = {255, 180, 200, 180};
        e.colorEnd = {240, 150, 180, 0};
        e.maxParticles = 400;
        break;

    case ParticlePreset::CUSTOM:
    default:
        e.name = "custom";
        break;
    }

    return e;
}

int ParticleSystem::addEmitter(const ParticleEmitter& emitter) {
    int idx = static_cast<int>(m_emitters.size());
    m_emitters.push_back(emitter);
    m_particles.emplace_back();
    m_particles.back().reserve(emitter.maxParticles);
    std::cout << "[Particles] Emitter added: " << emitter.name << " (rate=" << emitter.spawnRate
              << ")\n";
    return idx;
}

int ParticleSystem::startPreset(ParticlePreset preset, float intensity, float wind) {
    auto emitter = makePreset(preset, intensity, wind);
    return addEmitter(emitter);
}

void ParticleSystem::stopEmitter(const std::string& name) {
    for (size_t i = 0; i < m_emitters.size(); i++) {
        if (m_emitters[i].name == name) {
            m_emitters[i].active = false;
            std::cout << "[Particles] Stopped: " << name << "\n";
        }
    }
}

void ParticleSystem::stopAll() {
    m_emitters.clear();
    m_particles.clear();
    std::cout << "[Particles] All emitters stopped.\n";
}

bool ParticleSystem::hasActiveEmitters() const {
    for (const auto& e : m_emitters) {
        if (e.active)
            return true;
    }
    return false;
}

void ParticleSystem::spawnParticle(ParticleEmitter& emitter, float screenW, float screenH) {
    Particle p;
    p.x = (emitter.areaX + randFloat(0, emitter.areaW)) * screenW;
    p.y = (emitter.areaY + randFloat(0, std::max(0.01f, emitter.areaH))) * screenH;

    float speed = randFloat(emitter.speedMin, emitter.speedMax);
    float angle = randFloat(emitter.angleMin, emitter.angleMax) * 3.14159f / 180.0f;
    p.vx = std::cos(angle) * speed + emitter.wind;
    p.vy = std::sin(angle) * speed;

    p.size = randFloat(emitter.sizeMin, emitter.sizeMax);
    p.life = randFloat(emitter.lifeMin, emitter.lifeMax);
    p.maxLife = p.life;
    p.rotation = randFloat(0, 360);
    p.rotSpeed = randFloat(emitter.rotSpeedMin, emitter.rotSpeedMax);
    p.color = emitter.colorStart;
    p.alpha = emitter.colorStart.a / 255.0f;

    auto& pool = m_particles[&emitter - &m_emitters[0]];
    if (pool.size() < emitter.maxParticles) {
        pool.push_back(p);
    } else {
        for (auto& existing : pool) {
            if (existing.life <= 0) {
                existing = p;
                return;
            }
        }
    }
}

void ParticleSystem::update(double dt, float screenW, float screenH) {
    float fdt = static_cast<float>(dt);

    for (int i = static_cast<int>(m_emitters.size()) - 1; i >= 0; i--) {
        if (!m_emitters[i].active) {
            bool hasLive = false;
            for (const auto& p : m_particles[i]) {
                if (p.life > 0) {
                    hasLive = true;
                    break;
                }
            }
            if (!hasLive) {
                m_emitters.erase(m_emitters.begin() + i);
                m_particles.erase(m_particles.begin() + i);
            }
        }
    }

    for (size_t ei = 0; ei < m_emitters.size(); ei++) {
        auto& emitter = m_emitters[ei];
        auto& pool = m_particles[ei];

        if (emitter.active) {
            emitter.spawnAccum += emitter.spawnRate * fdt;
            while (emitter.spawnAccum >= 1.0f) {
                spawnParticle(emitter, screenW, screenH);
                emitter.spawnAccum -= 1.0f;
            }
        }

        for (auto& p : pool) {
            if (p.life <= 0)
                continue;

            p.life -= fdt;
            if (p.life <= 0) {
                p.life = 0;
                continue;
            }

            p.vy += emitter.gravity * fdt;
            p.vx += emitter.wind * fdt * 0.1f;
            p.x += p.vx * fdt;
            p.y += p.vy * fdt;
            p.rotation += p.rotSpeed * fdt;

            float lifeRatio = 1.0f - (p.life / p.maxLife);
            p.color.r = lerpByte(emitter.colorStart.r, emitter.colorEnd.r, lifeRatio);
            p.color.g = lerpByte(emitter.colorStart.g, emitter.colorEnd.g, lifeRatio);
            p.color.b = lerpByte(emitter.colorStart.b, emitter.colorEnd.b, lifeRatio);
            p.alpha =
                (emitter.colorStart.a + (emitter.colorEnd.a - emitter.colorStart.a) * lifeRatio) /
                255.0f;
        }
    }
}

void ParticleSystem::render(SDL_Renderer* renderer) {
    SDL_SetRenderDrawBlendMode(renderer, SDL_BLENDMODE_BLEND);

    for (size_t ei = 0; ei < m_emitters.size(); ei++) {
        const auto& emitter = m_emitters[ei];
        const auto& pool = m_particles[ei];

        for (const auto& p : pool) {
            if (p.life <= 0)
                continue;

            uint8_t a = static_cast<uint8_t>(std::clamp(p.alpha * 255.0f, 0.0f, 255.0f));
            if (a == 0)
                continue;

            SDL_SetRenderDrawColor(renderer, p.color.r, p.color.g, p.color.b, a);

            if (p.size <= 2.0f) {
                SDL_FRect r = {p.x, p.y, p.size, p.size * 3.0f};
                if (emitter.preset == ParticlePreset::RAIN) {
                    SDL_RenderFillRect(renderer, &r);
                } else {
                    SDL_FRect sq = {p.x - p.size * 0.5f, p.y - p.size * 0.5f, p.size, p.size};
                    SDL_RenderFillRect(renderer, &sq);
                }
            } else {
                float half = p.size * 0.5f;
                SDL_FRect r = {p.x - half, p.y - half, p.size, p.size};
                SDL_RenderFillRect(renderer, &r);
            }
        }
    }
}

} // namespace velora::render
