
#define NOMINMAX
#define MINIAUDIO_IMPLEMENTATION
#include <miniaudio.h>

#include "AudioMixer.h"
#include <iostream>
#include <cmath>
#include <algorithm>

namespace velora::audio {

AudioMixer::AudioMixer() = default;

AudioMixer::~AudioMixer() {
    if (m_initialized)
        shutdown();
}

bool AudioMixer::initialize() {
    m_engine = new ma_engine();

    ma_engine_config config = ma_engine_config_init();
    config.channels = 2;
    config.sampleRate = 48000;

    ma_result result = ma_engine_init(&config, m_engine);
    if (result != MA_SUCCESS) {
        std::cerr << "[AudioMixer] Failed to initialize miniaudio engine (error: " << result
                  << ")\n";
        delete m_engine;
        m_engine = nullptr;
        return false;
    }

    m_initialized = true;
    std::cout << "[AudioMixer] miniaudio engine initialized (48kHz stereo).\n";
    return true;
}

void AudioMixer::shutdown() {
    for (auto& [id, track] : m_tracks) {
        if (track.sound) {
            ma_sound_uninit(track.sound);
            delete track.sound;
        }
    }
    m_tracks.clear();

    if (m_engine) {
        ma_engine_uninit(m_engine);
        delete m_engine;
        m_engine = nullptr;
    }
    m_initialized = false;
}

void AudioMixer::update(double dt) {
    if (!m_initialized)
        return;

    bool voicePlaying = isVoicePlaying();
    float targetDuck = (m_duckingEnabled && voicePlaying) ? m_duckingLevel : 1.0f;
    float duckSpeed = 3.0f * static_cast<float>(dt);
    m_currentDuck += (targetDuck - m_currentDuck) * std::min(duckSpeed, 1.0f);

    for (auto it = m_tracks.begin(); it != m_tracks.end();) {
        auto& track = it->second;

        if (track.sound && !track.looping && ma_sound_at_end(track.sound)) {
            ma_sound_uninit(track.sound);
            delete track.sound;
            it = m_tracks.erase(it);
            continue;
        }

        if (track.fadeSpeed != 0.0f) {
            track.volume += track.fadeSpeed * static_cast<float>(dt);

            if (track.fadeSpeed > 0 && track.volume >= track.targetVolume) {
                track.volume = track.targetVolume;
                track.fadeSpeed = 0.0f;
            } else if (track.fadeSpeed < 0 && track.volume <= track.targetVolume) {
                track.volume = track.targetVolume;
                track.fadeSpeed = 0.0f;
                if (track.volume <= 0.0f) {
                    if (track.sound) {
                        ma_sound_uninit(track.sound);
                        delete track.sound;
                    }
                    it = m_tracks.erase(it);
                    continue;
                }
            }
        }

        if (track.sound) {
            float channelVol = getChannelVolume(track.channel);
            float duckMult = (track.channel == AudioChannel::BGM) ? m_currentDuck : 1.0f;
            float finalVol = m_masterVolume * channelVol * track.volume * duckMult;
            ma_sound_set_volume(track.sound, finalVol);
        }

        ++it;
    }
}

void AudioMixer::play(const std::string& channel, const std::string& filePath, double fadeIn,
                      double volume, double crossfade) {
    if (!m_initialized || !m_engine)
        return;

    AudioChannel ch = parseChannel(channel);
    std::string trackId = channel;

    if (crossfade > 0.0 && m_tracks.count(trackId)) {
        auto& old = m_tracks[trackId];
        old.targetVolume = 0.0f;
        old.fadeSpeed = -old.volume / static_cast<float>(crossfade);
        std::string oldId = trackId + "_old_" + std::to_string(++m_trackSequence);
        m_tracks[oldId] = std::move(old);
        m_tracks.erase(trackId);
    } else if (m_tracks.count(trackId)) {
        auto& old = m_tracks[trackId];
        if (old.sound) {
            ma_sound_uninit(old.sound);
            delete old.sound;
        }
        m_tracks.erase(trackId);
    }

    ma_sound* sound = new ma_sound();
    ma_uint32 flags = (ch == AudioChannel::BGM) ? (MA_SOUND_FLAG_STREAM | MA_SOUND_FLAG_ASYNC)
                                                : (MA_SOUND_FLAG_DECODE | MA_SOUND_FLAG_ASYNC);
    ma_result result =
        ma_sound_init_from_file(m_engine, filePath.c_str(), flags, nullptr, nullptr, sound);

    if (result != MA_SUCCESS) {
        std::cerr << "[AudioMixer] Audio file unavailable, skipping playback: " << filePath
                  << " (error: " << result << ")\n";
        delete sound;
        // Missing optional audio must not stop script execution.
        return;
    }

    bool loop = (ch == AudioChannel::BGM);
    ma_sound_set_looping(sound, loop);

    AudioTrack track;
    track.id = trackId;
    track.filePath = filePath;
    track.channel = ch;
    track.sound = sound;
    track.looping = loop;
    track.targetVolume = static_cast<float>(volume);
    track.playing = true;

    if (fadeIn > 0.0) {
        track.volume = 0.0f;
        track.fadeSpeed = static_cast<float>(volume / fadeIn);
        ma_sound_set_volume(sound, 0.0f);
    } else {
        track.volume = static_cast<float>(volume);
        float finalVol = m_masterVolume * getChannelVolume(ch) * track.volume;
        ma_sound_set_volume(sound, finalVol);
    }

    ma_sound_start(sound);
    m_tracks[trackId] = std::move(track);

    std::cout << "[AudioMixer] Playing: " << filePath << " on " << channel
              << (loop ? " (seamless loop)" : "") << "\n";
}

void AudioMixer::stop(const std::string& channel, double fadeOut) {
    auto it = m_tracks.find(channel);
    if (it == m_tracks.end())
        return;

    if (fadeOut > 0.0) {
        it->second.targetVolume = 0.0f;
        it->second.fadeSpeed = -it->second.volume / static_cast<float>(fadeOut);
    } else {
        if (it->second.sound) {
            ma_sound_uninit(it->second.sound);
            delete it->second.sound;
        }
        m_tracks.erase(it);
    }
}

void AudioMixer::stopAll(double fadeOut) {
    for (auto& [id, track] : m_tracks) {
        if (fadeOut > 0.0) {
            track.targetVolume = 0.0f;
            track.fadeSpeed = -track.volume / static_cast<float>(fadeOut);
        }
    }
    if (fadeOut <= 0.0) {
        for (auto& [id, track] : m_tracks) {
            if (track.sound) {
                ma_sound_uninit(track.sound);
                delete track.sound;
            }
        }
        m_tracks.clear();
    }
}

void AudioMixer::playSFX(const std::string& filePath, float volume) {
    play("sfx_" + std::to_string(++m_trackSequence), filePath, 0.0, volume, 0.0);
}

bool AudioMixer::isVoicePlaying() const {
    auto it = m_tracks.find("voice");
    if (it == m_tracks.end())
        return false;
    if (!it->second.sound)
        return false;
    return ma_sound_is_playing(it->second.sound);
}

double AudioMixer::getAudioDuration(const std::string& filePath) const {
    ma_decoder decoder;
    if (ma_decoder_init_file(filePath.c_str(), nullptr, &decoder) != MA_SUCCESS)
        return 0.0;
    ma_uint64 frames = 0;
    const ma_result result = ma_decoder_get_length_in_pcm_frames(&decoder, &frames);
    const ma_uint32 sampleRate = decoder.outputSampleRate;
    ma_decoder_uninit(&decoder);
    if (result != MA_SUCCESS || sampleRate == 0)
        return 0.0;
    return static_cast<double>(frames) / static_cast<double>(sampleRate);
}

void AudioMixer::setMasterVolume(float vol) {
    m_masterVolume = std::clamp(vol, 0.0f, 1.0f);
}
void AudioMixer::setBGMVolume(float vol) {
    m_bgmVolume = std::clamp(vol, 0.0f, 1.0f);
}
void AudioMixer::setSFXVolume(float vol) {
    m_sfxVolume = std::clamp(vol, 0.0f, 1.0f);
}
void AudioMixer::setVoiceVolume(float vol) {
    m_voiceVolume = std::clamp(vol, 0.0f, 1.0f);
}

float AudioMixer::getChannelVolume(AudioChannel ch) const {
    switch (ch) {
    case AudioChannel::BGM:
        return m_bgmVolume;
    case AudioChannel::SFX:
        return m_sfxVolume;
    case AudioChannel::VOICE:
        return m_voiceVolume;
    default:
        return 1.0f;
    }
}

AudioChannel AudioMixer::parseChannel(const std::string& ch) const {
    if (ch == "bgm")
        return AudioChannel::BGM;
    if (ch == "sfx" || ch.starts_with("sfx_"))
        return AudioChannel::SFX;
    if (ch == "voice")
        return AudioChannel::VOICE;
    return AudioChannel::SFX;
}

} // namespace velora::audio
