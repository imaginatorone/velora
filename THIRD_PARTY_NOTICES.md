# Third-party notices

Velora fetches the following libraries from pinned source revisions during
configuration. They are not stored in this repository.

| Project | Purpose | License |
| --- | --- | --- |
| [SDL](https://github.com/libsdl-org/SDL) | Platform, window, and input layer | zlib |
| [SDL_image](https://github.com/libsdl-org/SDL_image) | Image decoding | zlib |
| [SDL_ttf](https://github.com/libsdl-org/SDL_ttf) | Font rendering | zlib |
| [miniaudio](https://github.com/mackron/miniaudio) | Audio playback | Public domain or MIT No Attribution |
| [JSON for Modern C++](https://github.com/nlohmann/json) | JSON and CBOR handling | MIT |

The exact revisions are listed in `CMakeLists.txt`. Installed packages include
the license text for each dependency under `licenses/`. Those licenses apply
to their respective components. Velora's own source is covered by the root
`LICENSE` file.
