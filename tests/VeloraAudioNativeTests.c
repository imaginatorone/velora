#include "velora_audio_native.h"

#include <stdint.h>
#include <stdio.h>
#include <string.h>

static int write_u16_le(FILE* file, uint16_t value) {
    const uint8_t bytes[2] = {(uint8_t)value, (uint8_t)(value >> 8)};
    return fwrite(bytes, sizeof(bytes), 1, file) == 1;
}

static int write_u32_le(FILE* file, uint32_t value) {
    const uint8_t bytes[4] = {
        (uint8_t)value,
        (uint8_t)(value >> 8),
        (uint8_t)(value >> 16),
        (uint8_t)(value >> 24),
    };
    return fwrite(bytes, sizeof(bytes), 1, file) == 1;
}

static int write_test_wav(const char* path) {
    enum {
        sample_rate = 48000,
        channels = 2,
        sample_count = 480,
        bytes_per_sample = 2,
    };
    const uint32_t data_bytes = sample_count * channels * bytes_per_sample;
    const int16_t silence[sample_count * channels] = {0};
    FILE* file = fopen(path, "wb");
    int ok;

    if (file == NULL) {
        return 0;
    }

    ok = fwrite("RIFF", 4, 1, file) == 1 &&
         write_u32_le(file, 36 + data_bytes) &&
         fwrite("WAVEfmt ", 8, 1, file) == 1 &&
         write_u32_le(file, 16) &&
         write_u16_le(file, 1) &&
         write_u16_le(file, channels) &&
         write_u32_le(file, sample_rate) &&
         write_u32_le(file, sample_rate * channels * bytes_per_sample) &&
         write_u16_le(file, channels * bytes_per_sample) &&
         write_u16_le(file, bytes_per_sample * 8) &&
         fwrite("data", 4, 1, file) == 1 &&
         write_u32_le(file, data_bytes) &&
         fwrite(silence, sizeof(silence), 1, file) == 1;
    ok = fclose(file) == 0 && ok;
    return ok;
}

static int require_success(int32_t result, const char* operation) {
    if (result == 0) {
        return 1;
    }

    fprintf(
        stderr,
        "%s failed: %d (%s)\n",
        operation,
        result,
        velora_audio_result_description(result));
    return 0;
}

int main(void) {
    const char* wav_path = "velora_audio_native_test.wav";
    velora_audio_engine* engine = NULL;
    velora_audio_sound* sound = NULL;
    velora_audio_sound* missing = NULL;
    double duration = 0.0;
    double cursor = 0.0;
    int32_t at_end = -1;
    int exit_code = 1;

    if (velora_audio_get_abi_version() != VELORA_AUDIO_ABI_VERSION ||
        strcmp(velora_audio_get_miniaudio_version(), "0.11.25") != 0 ||
        !write_test_wav(wav_path)) {
        goto cleanup;
    }

    if (!require_success(velora_audio_engine_create(48000, 2, 1, &engine), "engine create") ||
        velora_audio_sound_open_file(engine, "does-not-exist.wav", 0, &missing) == 0 ||
        missing != NULL ||
        !require_success(velora_audio_sound_open_file(engine, wav_path, 0, &sound), "sound open") ||
        !require_success(velora_audio_sound_set_looping(sound, 0), "set looping") ||
        !require_success(velora_audio_sound_set_volume(sound, 0.5f), "set volume") ||
        !require_success(velora_audio_sound_start(sound), "start") ||
        !require_success(velora_audio_sound_get_duration_seconds(sound, &duration), "duration") ||
        duration < 0.009 || duration > 0.011 ||
        !require_success(velora_audio_sound_seek_seconds(sound, 0.005), "seek") ||
        !require_success(velora_audio_sound_get_cursor_seconds(sound, &cursor), "cursor") ||
        cursor < 0.004 || cursor > 0.006 ||
        !require_success(velora_audio_sound_at_end(sound, &at_end), "at end") ||
        at_end != 0 ||
        !require_success(velora_audio_sound_stop(sound), "stop") ||
        !require_success(velora_audio_engine_suspend(engine), "suspend") ||
        !require_success(velora_audio_engine_resume(engine), "resume")) {
        goto cleanup;
    }

    exit_code = 0;

cleanup:
    velora_audio_sound_destroy(sound);
    velora_audio_sound_destroy(missing);
    velora_audio_engine_destroy(engine);
    remove(wav_path);
    return exit_code;
}
