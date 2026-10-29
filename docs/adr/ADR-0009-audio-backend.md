# ADR-0009: First-Party miniaudio Backend Boundary

- Status: Accepted with production gates
- Date: 2026-08-22

## Context

Velora already owns deterministic channel state, fades, crossfades, channel
gain, and voice ducking in its platform-neutral audio model. The production
backend therefore needs reliable decoding, streaming, playback cursors,
device lifecycle, and cross-platform packaging; it must not introduce a
second mixer policy or expose native handles to Runtime.

The C++ compatibility oracle uses miniaudio at 48 kHz stereo, streams BGM,
decodes other tracks asynchronously, polls completion, and preserves
loop/cursor behavior. Finished audio and missing optional assets must never
block narrative execution. Android and iOS also require host-owned audio-focus
and session policy, packaged-asset access that is not limited to filesystem
paths, and AOT-safe interop.

Available managed wrappers, SDL audio/SDL_mixer, and a first-party boundary
were evaluated for current maintenance, supported targets, native ownership,
codec surface, trimming/AOT behavior, and licensing on 2026-08-22.

## Decision

1. The production implementation is a first-party `Velora.Audio.Miniaudio`
   adapter over a small Velora-owned C ABI shim. The native implementation
   builds against miniaudio **0.11.25**, upstream commit `9634bed`, and selects
   its MIT-0 license option. The current CMake build fetches the exact revision
   or accepts a local source directory, then verifies the header SHA-256;
   upstream source is not checked into this repository.
2. Raw `ma_engine`, `ma_sound`, device, and decoder structs never cross the C
   ABI. The shim owns opaque engine/sound allocations and exports only stable
   operations required by Velora: create/destroy, open, start/stop, gain,
   looping, completion polling, duration/cursor/seek, suspend/resume, version,
   and typed result text.
3. Managed interop uses source-generated `[LibraryImport]`, explicit
   idempotent ownership, and a native-version handshake. There is no managed
   callback from the real-time audio thread. Completion and errors are polled
   on the Player thread; native destruction never occurs from an audio
   callback.
4. `AudioMixerState` remains the sole owner of fade, crossfade, channel-gain,
   and ducking semantics. The backend applies the final per-track gain and
   reports completed track IDs and actionable errors. Before native integration,
   the backend contract must gain typed open/apply results plus duration,
   cursor, seek, and completion polling.
5. BGM uses streaming plus asynchronous decode. SFX and voice use asynchronous
   decode. The engine requests 48 kHz stereo and disables spatialization for
   compatibility; the native device may perform required platform conversion.
6. WAV, FLAC, and MP3 use miniaudio's built-in decoder path. Ogg Vorbis support
   statically vendors libogg **1.3.6** and libvorbis **1.3.7** into the single
   Velora native artifact and registers miniaudio's libvorbis decoder backend.
   Their BSD-3-Clause notices are shipped with the miniaudio MIT-0 text.
7. Ogg is a production gate, not an unconditional safety claim. Malformed-input
   crash/undefined-behavior reports in current native decoders require reviewed
   fixes or a newer vetted pin plus ASan/UBSan fuzzing before release. If that
   gate cannot be met, Ogg export remains blocked rather than silently using an
   unreviewed decoder.
8. Each target ships exactly one Velora-owned native audio artifact:
   `velora_audio_native.dll`, `.so`, or `.dylib` for desktop; per-ABI Android
   `.so`; and a statically linked iOS library/XCFramework. iOS builds disable
   runtime framework lookup. Android packaging must accept file-descriptor /
   offset / length sources and pass 16-KiB page-alignment validation.
9. Platform hosts own Android AudioFocus and Apple AVAudioSession behavior.
   They translate platform callbacks onto the Player thread and then call the
   backend's suspend/resume/interruption contract. Runtime and the neutral
   audio model never reference those platform APIs.
10. Exact vendored source revisions, source hashes, compiler flags, native
    dependency notices, and the codec corpus are release-controlled inputs.
    A wrapper package may only replace this decision through a superseding ADR
    with equivalent desktop/mobile/AOT and codec-security evidence.

## Alternatives considered

### Broad managed miniaudio wrappers

`JAJ.Packages.MiniAudioEx` was the strongest current wrapper candidate, but it
exposes a much larger modified-native surface, does not publish an equivalent
trim/AOT compatibility contract, and its evaluated iOS packaging uses a
dynamic framework. Other current wrappers were older, desktop-only, or lacked
the required mobile evidence. They remain prototype references, not the
production boundary.

### SDL core audio or SDL_mixer

SDL core supplies device and PCM-stream primitives but not the complete codec,
streaming, seek, and lifecycle behavior Velora needs. SDL_mixer supplies more
formats but introduces a second mixer policy and another native codec/package
matrix while duplicating semantics already owned by `AudioMixerState`.

### Fully managed decoders

Managed MP3/Vorbis libraries reduce native decoder surface, but production
streaming would still require a managed decode-worker and native ring-buffer
design. Current Vorbis choices also do not provide a sufficiently established
stable/AOT/mobile release contract for the first parity backend. This remains
a useful later security experiment.

### Raw miniaudio struct bindings

Rejected. Upstream does not guarantee patch-level ABI stability for transparent
structs, whose addresses and layout are part of correct ownership. A small
opaque shim gives Velora one stable boundary and supports static NativeAOT/iOS
linking without duplicating native structure definitions in C#.

## Consequences

- Velora owns a small native build and security surface instead of inheriting a
  broad wrapper's API and release cadence.
- Deterministic audio policy stays testable without a device; the native
  adapter can be tested independently against a null backend and codec corpus.
- Native source, codecs, per-target builds, notices, sanitizer jobs, and mobile
  packaging add release work. These are explicit costs of supporting untrusted
  narrative assets on all required platforms.
- The current machine cannot validate Android/iOS .NET packaging or real-device
  focus/interruption behavior. CI compile/link checks and device evidence remain
  mandatory before those targets can be claimed.

## Required verification

As of 2026-10-28 the desktop C shim, generated P/Invoke adapter and managed
contract tests build. Native CTest and a managed native-interoperability test
exercise a generated WAV with no device, including Unicode path, seek, cursor,
suspend/resume and teardown. The Ogg/libvorbis path, mobile artifacts and
real-device/mixer integration below remain production gates, not completed work.

- Null-device and real-device lifecycle tests; idempotent teardown.
- WAV/FLAC/MP3/Ogg decode, duration, seek, cursor, loop, Unicode source, missing
  and corrupt input, crossfade overlap, and completion polling.
- Suspend/resume cursor preservation and host interruption policies.
- ASan/UBSan plus malformed codec fuzz corpus, with Ogg as a release gate.
- Desktop NativeAOT publish smoke and artifact/notices inspection per RID.
- Android ABI package/link/16-KiB checks and iOS device/simulator static-link
  checks, followed by real-device audio focus/session tests.

## Primary references

- [miniaudio 0.11.25 release](https://github.com/mackron/miniaudio/releases/tag/0.11.25)
- [miniaudio manual](https://miniaud.io/docs/manual/index.html)
- [miniaudio license](https://github.com/mackron/miniaudio/blob/0.11.25/LICENSE)
- [.NET source-generated P/Invoke](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke-source-generation)
- [.NET NativeAOT native interop](https://learn.microsoft.com/dotnet/core/deploying/native-aot/interop)
- [Xiph stable downloads](https://www.xiph.org/downloads/)
- [Android audio focus](https://developer.android.com/media/optimize/audio-focus)
- [Android 16-KiB page-size guidance](https://developer.android.com/guide/practices/page-sizes)
