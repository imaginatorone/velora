<p align="center">
  <img src="assets/icons/velora.svg" width="112" alt="Velora Engine logo">
</p>

<h1 align="center">Velora Engine</h1>

<p align="center">
  A compact C++20 runtime for building 2D visual novels on Windows, Linux, and macOS.
</p>

<p align="center">
  <a href="https://github.com/imaginatorone/velora/actions/workflows/ci.yml"><img src="https://github.com/imaginatorone/velora/actions/workflows/ci.yml/badge.svg" alt="CI status"></a>
  <img src="https://img.shields.io/badge/C%2B%2B-20-59636F" alt="C++20">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-D9655D" alt="Windows, Linux, and macOS">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-B8A98F" alt="MIT License"></a>
</p>

Velora combines SDL 3, SDL_image, SDL_ttf, miniaudio, and a small scenario
language called VelScript. This repository contains the engine, an asset-free
example, and regression tests. It does not contain a game or production
artwork.

## Project status

Velora is in active `0.x` development. Story flow, rendering, audio, saves,
settings, and localization are implemented. Public APIs and VelScript syntax
may still change before `1.0`.

Video playback, encrypted asset archives, and network APIs are not currently
implemented.

## Features

- dialogue, choices, text input, variables, conditions, labels, and jumps
- backgrounds, layered sprites, transitions, tweens, particles, and post effects
- music, sound effects, and voice channels
- save slots, quick save and load, persistent settings, and dialogue history
- windowed, borderless, and fullscreen modes
- UTF-8 scripts and external locale files
- native CMake builds for Windows, Linux, and macOS

## Build

You need CMake 3.25 or newer, Git, and a C++20 compiler. The first configure
downloads dependencies at revisions pinned in `CMakeLists.txt`.

```sh
cmake --preset release
cmake --build --preset release --parallel
ctest --preset release -C Release
```

The executable is written below `out/build/release/bin`. On macOS it is an app
bundle. On Windows and Linux, keep the copied `assets` directory beside the
executable.

Platform prerequisites, install layouts, and universal macOS builds are
documented in [BUILDING.md](BUILDING.md).

## Start a story

Velora opens [assets/scripts/example.vel](assets/scripts/example.vel) by
default. Pass another entry file with:

```sh
VeloraEngine --script path/to/story.vel
```

VelScript files use the `.vel` extension:

```text
character guide = "Guide" color: #8FB3A2

label start:
    guide "Welcome to Velora."

    menu:
        "Continue":
            jump next_scene
        "Leave":
            jump finish
    end

label next_scene:
    guide "The story continues."
    jump finish

label finish:
    "End of example."
```

Read the [VelScript guide](docs/velscript.md) for the language walkthrough,
syntax reference, examples, and common mistakes.

## Controls

| Key | Action |
| --- | --- |
| Enter or Space | Advance dialogue |
| Escape | Open or close the game menu |
| A | Toggle auto mode |
| Ctrl | Hold to skip |
| Page Up | Open dialogue history |
| F5 / F7 | Quick save / quick load |
| Shift+R | Reload the current script |
| F11 | Toggle fullscreen |

## Documentation

| Document | Purpose |
| --- | --- |
| [VelScript guide](docs/velscript.md) | Language tutorial and command reference |
| [Build guide](BUILDING.md) | Platform setup, builds, installs, and packaging |
| [Contributing guide](CONTRIBUTING.md) | Development workflow and pull request checks |
| [Governance](GOVERNANCE.md) | Project roles and decision process |
| [Security policy](SECURITY.md) | Private vulnerability reporting |
| [Third-party notices](THIRD_PARTY_NOTICES.md) | Licenses for fetched dependencies |

## Repository layout

```text
.github/         CI and contribution templates
assets/          Runtime icons, locale files, and example scripts
cmake/           CMake compatibility modules
docs/            VelScript documentation
src/             Engine source
tests/           Parser and save-format regression tests
```

Generated files belong under `out/` and are ignored by Git.

## Contributing

Contributions are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md), and
open an issue before making a large behavioral or language change. CI builds
and tests every pull request on Windows, Linux, and macOS.

## License

Velora Engine is released under the [MIT License](LICENSE). You may use,
modify, and distribute the engine in personal or commercial projects,
including closed-source games. Keep the copyright and license notice with
copies or substantial portions of the engine.

Your scripts, artwork, audio, and other game content remain yours and may use
any license you choose. The MIT license applies to Velora itself, not to work
created with it. Dependency licenses are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
