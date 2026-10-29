#ifndef VELORA_AUDIO_NATIVE_H
#define VELORA_AUDIO_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#define VELORA_AUDIO_EXPORT __declspec(dllexport)
#elif defined(__GNUC__) || defined(__clang__)
#define VELORA_AUDIO_EXPORT __attribute__((visibility("default")))
#else
#define VELORA_AUDIO_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct velora_audio_engine velora_audio_engine;
typedef struct velora_audio_sound velora_audio_sound;

enum {
    VELORA_AUDIO_ABI_VERSION = 1,
};

/*
 * All operational functions return a miniaudio ma_result value. The ABI keeps
 * miniaudio structs opaque and never invokes managed code from an audio or
 * decode worker thread.
 */
VELORA_AUDIO_EXPORT uint32_t velora_audio_get_abi_version(void);
VELORA_AUDIO_EXPORT const char* velora_audio_get_miniaudio_version(void);
VELORA_AUDIO_EXPORT const char* velora_audio_result_description(int32_t result);

VELORA_AUDIO_EXPORT int32_t velora_audio_engine_create(
    uint32_t sample_rate,
    uint32_t channels,
    int32_t no_device,
    velora_audio_engine** engine);
VELORA_AUDIO_EXPORT void velora_audio_engine_destroy(velora_audio_engine* engine);
VELORA_AUDIO_EXPORT int32_t velora_audio_engine_suspend(velora_audio_engine* engine);
VELORA_AUDIO_EXPORT int32_t velora_audio_engine_resume(velora_audio_engine* engine);

VELORA_AUDIO_EXPORT int32_t velora_audio_sound_open_file(
    velora_audio_engine* engine,
    const char* path_utf8,
    int32_t streaming,
    velora_audio_sound** sound);
VELORA_AUDIO_EXPORT void velora_audio_sound_destroy(velora_audio_sound* sound);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_start(velora_audio_sound* sound);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_stop(velora_audio_sound* sound);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_set_volume(
    velora_audio_sound* sound,
    float volume);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_set_looping(
    velora_audio_sound* sound,
    int32_t looping);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_at_end(
    const velora_audio_sound* sound,
    int32_t* at_end);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_get_duration_seconds(
    const velora_audio_sound* sound,
    double* seconds);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_get_cursor_seconds(
    const velora_audio_sound* sound,
    double* seconds);
VELORA_AUDIO_EXPORT int32_t velora_audio_sound_seek_seconds(
    velora_audio_sound* sound,
    double seconds);

#ifdef __cplusplus
}
#endif

#endif
