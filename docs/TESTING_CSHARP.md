# Testing the C# migration

The current executable is a headless CLI using the production compiler and
Runtime. It is not yet the visual Player or Editor. Use the SDK in `global.json`.

## Build and test

From the repository root:

```sh
dotnet restore Velora.sln --locked-mode
dotnet build Velora.sln -c Debug --no-restore
dotnet test --solution Velora.sln -c Debug --no-build --no-restore
```

The solution uses Microsoft Testing Platform. On a restricted host without
MSBuild worker support, add `--disable-build-servers /m:1 /nodeReuse:false`
to build commands. Each generated `tests/*/bin/Debug/net10.0/*.Tests.dll`
also runs directly with `dotnet`.

## Validate and execute a story without a window

```sh
dotnet run --project src/Velora.Cli -- validate samples/KitchenSink/game.velproj
dotnet run --project src/Velora.Cli -- simulate samples/KitchenSink/game.velproj --input Reader
dotnet run --project src/Velora.Cli -- simulate samples/KitchenSink/game.velproj --choice 1 --input Reader
```

Simulation advances dialogue, submits input, selects the requested zero-based
choice in every menu, and advances each explicit wait. It uses fixed platform
values (`Reader`, `en`, `12:00`). Filesystem/window host actions are suppressed;
no audio or graphics device is opened. The default limit is 10,000 suspended
steps, in addition to the Runtime instruction limit. Exit codes: 0 success,
1 validation/runtime/I/O failure, 2 invalid command arguments or choice index.

## Self-contained desktop CLI artifacts

```sh
dotnet publish src/Velora.Cli -c Release -r osx-arm64 --self-contained true -o artifacts/cli/osx-arm64
dotnet publish src/Velora.Cli -c Release -r win-x64 --self-contained true -o artifacts/cli/win-x64
dotnet publish src/Velora.Cli -c Release -r linux-x64 --self-contained true -o artifacts/cli/linux-x64
dotnet restore Velora.sln
dotnet restore Velora.sln --locked-mode
```

Keep the entire publish directory together. On macOS/Linux run `Velora.Cli`;
on Windows run `Velora.Cli.exe`. No installed .NET runtime is required. These
CLI artifacts have no SDL/miniaudio dependency. Cross-publication from macOS
does not prove execution on Windows/Linux. Artifacts are ignored by Git.
RID publication temporarily adds that target to NuGet's project lock metadata;
the final ordinary restore returns solution lock files to their portable form,
then locked restore verifies it. Do not use the portable lock files with
`RestoreLockedMode=true` during a new RID publication.

## Real native audio interop without a sound device

```sh
cmake -S native/audio -B artifacts/native-audio -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts/native-audio --config Release
ctest --test-dir artifacts/native-audio -C Release --output-on-failure
```

The native build fetches the pinned miniaudio revision and verifies its header
hash. Offline builds may supply
`-DVELORA_MINIAUDIO_SOURCE_DIR=/absolute/path/to/miniaudio`; the same hash check
still applies. No SDL build is required.

Set `VELORA_AUDIO_NATIVE_PATH` to the absolute built library path before running
`Velora.Audio.Miniaudio.Tests`: `libvelora_audio_native.dylib` on macOS,
`libvelora_audio_native.so` on Linux, or `velora_audio_native.dll` on Windows
(typically under the `Release` subdirectory with Visual Studio generators).
Without this variable the native interoperability test explicitly skips.

The test opens a generated WAV using a Unicode filename and verifies duration,
seek/cursor, suspend/resume and teardown through the actual C ABI. It uses
miniaudio's no-device engine: passing does not claim audible output or the
unimplemented Ogg/mobile production gates.

SDL's optional smoke test still requires native SDL3/SDL3_image libraries,
`VELORA_RUN_SDL_NATIVE_SMOKE=1`, and execution on the OS main thread. Managed
backend tests cover ownership, caching, transitions and readback contracts but
do not replace that native check.
