
#pragma once

#include <string>
#include <unordered_map>
#include <vector>
#include <memory>
#include <cstdint>

struct ma_engine;
struct ma_sound;

namespace velora::audio {

enum class AudioChannel { BGM, SFX, VOICE };

struct AudioTrack {
    std::string id;
    std::string filePath;
    AudioChannel channel;
    float volume = 1.0f;
    float targetVolume = 1.0f;
    float fadeSpeed = 0.0f;
    bool playing = false;
    bool looping = false;
    ma_sound* sound = nullptr;
};

class AudioMixer {
  public:
    AudioMixer();
    ~AudioMixer();

    [[nodiscard]] bool initialize();

    void shutdown();

    void update(double dt);

    void play(const std::string& channel, const std::string& filePath, double fadeIn = 0.0,
              double volume = 1.0, double crossfade = 0.0);

    void stop(const std::string& channel, double fadeOut = 0.0);

    void stopAll(double fadeOut = 0.0);

    void playSFX(const std::string& filePath, float volume = 1.0f);

    void setDuckingEnabled(bool enabled) { m_duckingEnabled = enabled; }
    void setDuckingLevel(float level) { m_duckingLevel = level; }

    [[nodiscard]] double getAudioDuration(const std::string& filePath) const;

    [[nodiscard]] bool isVoicePlaying() const;

    void setMasterVolume(float vol);
    void setBGMVolume(float vol);
    void setSFXVolume(float vol);
    void setVoiceVolume(float vol);
    [[nodiscard]] float getMasterVolume() const { return m_masterVolume; }

  private:
    AudioChannel parseChannel(const std::string& ch) const;
    float getChannelVolume(AudioChannel ch) const;

    ma_engine* m_engine = nullptr;
    bool m_initialized = false;

    std::unordered_map<std::string, AudioTrack> m_tracks;

    float m_masterVolume = 1.0f;
    float m_bgmVolume = 0.7f;
    float m_sfxVolume = 1.0f;
    float m_voiceVolume = 1.0f;

    bool m_duckingEnabled = true;
    float m_duckingLevel = 0.3f;
    float m_currentDuck = 1.0f;
    uint64_t m_trackSequence = 0;
};

} // namespace velora::audio
