#define MINIAUDIO_IMPLEMENTATION
#include <miniaudio.h>

#include "velora_audio_native.h"

#include <float.h>
#include <math.h>
#include <stdlib.h>

struct velora_audio_engine {
    ma_engine value;
    ma_bool32 initialized;
    ma_bool32 no_device;
};

struct velora_audio_sound {
    ma_sound value;
    ma_bool32 initialized;
};

uint32_t velora_audio_get_abi_version(void) {
    return VELORA_AUDIO_ABI_VERSION;
}

const char* velora_audio_get_miniaudio_version(void) {
    return ma_version_string();
}

const char* velora_audio_result_description(int32_t result) {
    return ma_result_description((ma_result)result);
}

int32_t velora_audio_engine_create(
    uint32_t sample_rate,
    uint32_t channels,
    int32_t no_device,
    velora_audio_engine** engine) {
    velora_audio_engine* created;
    ma_engine_config config;
    ma_result result;

    if (engine == NULL || sample_rate == 0 || channels == 0) {
        return MA_INVALID_ARGS;
    }

    *engine = NULL;
    created = (velora_audio_engine*)calloc(1, sizeof(*created));
    if (created == NULL) {
        return MA_OUT_OF_MEMORY;
    }

    config = ma_engine_config_init();
    config.channels = channels;
    config.sampleRate = sample_rate;
    config.noDevice = no_device != 0 ? MA_TRUE : MA_FALSE;

    result = ma_engine_init(&config, &created->value);
    if (result != MA_SUCCESS) {
        free(created);
        return result;
    }

    created->initialized = MA_TRUE;
    created->no_device = config.noDevice;
    *engine = created;
    return MA_SUCCESS;
}

void velora_audio_engine_destroy(velora_audio_engine* engine) {
    if (engine == NULL) {
        return;
    }

    if (engine->initialized) {
        ma_engine_uninit(&engine->value);
        engine->initialized = MA_FALSE;
    }

    free(engine);
}

int32_t velora_audio_engine_suspend(velora_audio_engine* engine) {
    if (engine == NULL || !engine->initialized) {
        return MA_DEVICE_NOT_INITIALIZED;
    }

    if (engine->no_device) {
        return MA_SUCCESS;
    }

    return ma_engine_stop(&engine->value);
}

int32_t velora_audio_engine_resume(velora_audio_engine* engine) {
    if (engine == NULL || !engine->initialized) {
        return MA_DEVICE_NOT_INITIALIZED;
    }

    if (engine->no_device) {
        return MA_SUCCESS;
    }

    return ma_engine_start(&engine->value);
}

int32_t velora_audio_sound_open_file(
    velora_audio_engine* engine,
    const char* path_utf8,
    int32_t streaming,
    velora_audio_sound** sound) {
    velora_audio_sound* created;
    ma_uint32 flags;
    ma_result result;

    if (engine == NULL || !engine->initialized || path_utf8 == NULL ||
        path_utf8[0] == '\0' || sound == NULL) {
        return MA_INVALID_ARGS;
    }

    *sound = NULL;
    created = (velora_audio_sound*)calloc(1, sizeof(*created));
    if (created == NULL) {
        return MA_OUT_OF_MEMORY;
    }

    flags = streaming != 0 ? MA_SOUND_FLAG_STREAM : MA_SOUND_FLAG_DECODE;
    flags |= MA_SOUND_FLAG_ASYNC;
    flags |= MA_SOUND_FLAG_WAIT_INIT;
    flags |= MA_SOUND_FLAG_NO_SPATIALIZATION;
    result = ma_sound_init_from_file(
        &engine->value,
        path_utf8,
        flags,
        NULL,
        NULL,
        &created->value);
    if (result != MA_SUCCESS) {
        free(created);
        return result;
    }

    created->initialized = MA_TRUE;
    *sound = created;
    return MA_SUCCESS;
}

void velora_audio_sound_destroy(velora_audio_sound* sound) {
    if (sound == NULL) {
        return;
    }

    if (sound->initialized) {
        ma_sound_uninit(&sound->value);
        sound->initialized = MA_FALSE;
    }

    free(sound);
}

int32_t velora_audio_sound_start(velora_audio_sound* sound) {
    if (sound == NULL || !sound->initialized) {
        return MA_INVALID_ARGS;
    }

    return ma_sound_start(&sound->value);
}

int32_t velora_audio_sound_stop(velora_audio_sound* sound) {
    if (sound == NULL || !sound->initialized) {
        return MA_INVALID_ARGS;
    }

    return ma_sound_stop(&sound->value);
}

int32_t velora_audio_sound_set_volume(velora_audio_sound* sound, float volume) {
    if (sound == NULL || !sound->initialized || !isfinite(volume)) {
        return MA_INVALID_ARGS;
    }

    ma_sound_set_volume(&sound->value, volume);
    return MA_SUCCESS;
}

int32_t velora_audio_sound_set_looping(velora_audio_sound* sound, int32_t looping) {
    if (sound == NULL || !sound->initialized) {
        return MA_INVALID_ARGS;
    }

    ma_sound_set_looping(&sound->value, looping != 0 ? MA_TRUE : MA_FALSE);
    return MA_SUCCESS;
}

int32_t velora_audio_sound_at_end(
    const velora_audio_sound* sound,
    int32_t* at_end) {
    if (sound == NULL || !sound->initialized || at_end == NULL) {
        return MA_INVALID_ARGS;
    }

    *at_end = ma_sound_at_end(&sound->value) ? 1 : 0;
    return MA_SUCCESS;
}

int32_t velora_audio_sound_get_duration_seconds(
    const velora_audio_sound* sound,
    double* seconds) {
    float duration = 0.0f;
    ma_result result;

    if (sound == NULL || !sound->initialized || seconds == NULL) {
        return MA_INVALID_ARGS;
    }

    result = ma_sound_get_length_in_seconds(&sound->value, &duration);
    if (result != MA_SUCCESS) {
        return result;
    }

    *seconds = (double)duration;
    return MA_SUCCESS;
}

int32_t velora_audio_sound_get_cursor_seconds(
    const velora_audio_sound* sound,
    double* seconds) {
    float cursor = 0.0f;
    ma_result result;

    if (sound == NULL || !sound->initialized || seconds == NULL) {
        return MA_INVALID_ARGS;
    }

    result = ma_sound_get_cursor_in_seconds(&sound->value, &cursor);
    if (result != MA_SUCCESS) {
        return result;
    }

    *seconds = (double)cursor;
    return MA_SUCCESS;
}

int32_t velora_audio_sound_seek_seconds(
    velora_audio_sound* sound,
    double seconds) {
    if (sound == NULL || !sound->initialized || !isfinite(seconds) || seconds < 0.0 ||
        seconds > (double)FLT_MAX) {
        return MA_INVALID_ARGS;
    }

    return ma_sound_seek_to_second(&sound->value, (float)seconds);
}
