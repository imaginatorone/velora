# Building Velora

For the C# migration's SDK, tests, headless CLI, desktop publish commands and
native audio interop checks, see [Testing the C# migration](docs/TESTING_CSHARP.md).
The instructions below build the legacy C++ runtime.

Velora targets 64-bit Windows, Linux, and macOS. CMake fetches pinned source
revisions for third-party libraries, so a system-wide SDL installation is not
required. The first configure needs Git and an internet connection.

## Supported toolchains

| Platform | Recommended toolchain |
| --- | --- |
| Windows | Visual Studio 2022 with MSVC |
| Linux | GCC or Clang with C++20 support |
| macOS | Xcode or Xcode Command Line Tools |

CMake 3.25 or newer is required on every platform.

## Windows

Install Git, CMake, and Visual Studio 2022 with the **Desktop development with
C++** workload. Run these commands from Developer PowerShell:

```powershell
cmake --preset release -G "Visual Studio 17 2022" -A x64
cmake --build --preset release --config Release --parallel
ctest --preset release -C Release
```

The executable and assets are placed in `out/build/release/bin/Release`.

## Linux

Install a compiler, CMake, Git, and the development packages used by SDL's
desktop backends. On Ubuntu or Debian:

```sh
sudo apt-get update
sudo apt-get install build-essential cmake git \
  libasound2-dev libpulse-dev libx11-dev libxext-dev libxrandr-dev \
  libxcursor-dev libxfixes-dev libxi-dev libxss-dev libxtst-dev \
  libxkbcommon-dev libdrm-dev libgbm-dev libgl1-mesa-dev \
  libgles2-mesa-dev libegl1-mesa-dev libdbus-1-dev libibus-1.0-dev \
  libudev-dev libfribidi-dev libthai-dev libusb-1.0-0-dev libpipewire-0.3-dev \
  libwayland-dev libdecor-0-dev liburing-dev fonts-dejavu-core

cmake --preset release
cmake --build --preset release --parallel
ctest --preset release -C Release
```

The executable is `out/build/release/bin/VeloraEngine`.

## macOS

Install Xcode Command Line Tools or the full Xcode application, then install
CMake. Build and run with:

```sh
cmake --preset release
cmake --build --preset release --parallel
ctest --preset release -C Release
open out/build/release/bin/VeloraEngine.app
```

To build a universal Apple Silicon and Intel bundle, use a separate build
directory:

```sh
cmake -S . -B out/build/universal \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64"
cmake --build out/build/universal --parallel
```

Code signing and notarization are left to the person distributing the app.

## Install layout

Use CMake's install step to create a directory suitable for packaging:

```sh
cmake --install out/build/release --config Release --prefix out/package
```

Windows and Linux packages contain the executable under `bin`, with `assets`
beside it. macOS packages contain `VeloraEngine.app`. The project license,
third-party notices, and dependency license texts are included in the install
root.

## Useful options

| Option | Default | Purpose |
| --- | --- | --- |
| `VELORA_BUILD_TESTS` | `ON` | Build the core regression tests |
| `VELORA_WARNINGS_AS_ERRORS` | `OFF` | Treat project warnings as errors |
| `CMAKE_BUILD_TYPE` | `RelWithDebInfo` | Build type for single-config generators |

Pass options during configure:

```sh
cmake --preset debug -DVELORA_WARNINGS_AS_ERRORS=ON
```

## Clean builds

All generated files live under `out/`. Remove the matching build directory
when switching compilers or after changing low-level CMake options.
