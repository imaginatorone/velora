# ADR-0003: SDL3 Rendering Backend and Managed Binding

- Status: Accepted
- Date: 2026-08-22

## Context

Velora requires one SDL3-backed presentation path for the standalone Player,
PreviewHost, automated rendering checks, and eventual Android/iOS hosts. The
language, compiler, Runtime, save state, and general rendering state must remain
independent of SDL handles and ABI details.

Phase 10 needs reliable 2D parity before Phase 22 introduces an SDL GPU
post-processing path. Owning a handwritten interop layer now would also make
Velora responsible for SDL event unions, callback calling conventions, Android
`SDLActivity`, iOS static linkage, and companion-library packaging on every
target.

The available SDL3 C# bindings were evaluated against .NET 10 support, source
generation, native platform coverage, SDL_image/SDL_ttf packaging, license,
mobile host requirements, and maintenance state on 2026-08-22.

## Decision

1. `Velora.Rendering.Sdl` uses
   [edwardgushchin/SDL3-CS](https://github.com/edwardgushchin/SDL3-CS), pinned
   exactly to managed package `SDL3-CS` version `3.4.14.1`. Dependency ranges
   are not permitted.
2. SDL types and handles remain private implementation details of
   `Velora.Rendering.Sdl`. No SDL type may cross a rendering abstraction,
   Runtime, IR, persistence, project, or Editor protocol boundary.
3. Platform hosts, rather than rendering abstractions, own the corresponding
   native packages. The verified package family is:

   | Purpose | Exact version |
   | --- | --- |
   | `SDL3-CS.{Platform}` | `3.4.14.1` |
   | `SDL3-CS.{Platform}.Image` | `3.4.4.10` |
   | `SDL3-CS.{Platform}.TTF` | `3.2.2.9` |

   `{Platform}` is `Windows`, `Linux`, `MacOS`, `Android`, or `iOS` as
   appropriate. A host references only packages required by its target.
4. Phase 10 uses SDL's hardware-accelerated Render API for the complete 2D
   fallback: a 1280×720 logical target, letterboxing, backgrounds, sprites,
   transforms, transitions, UI, text, readback, and explicit resource
   ownership.
5. Phase 22 may add SDL GPU behind capability probing while retaining the 2D
   backend. Shaders are precompiled for target formats. `SDL_shadercross` is
   not a Phase 10 runtime dependency.
6. Asset and font data enter through bounded Velora-owned streams/bytes and
   SDL IO streams. VelScript never supplies an unrestricted native filesystem
   path.
7. Window, renderer, texture, surface, IO-stream, and font resources have
   explicit idempotent ownership and disposal on the SDL/render thread.
   Thread-affine destruction must not rely on finalizers.
8. The immutable upstream release tag and downloaded package hashes are
   verified as part of dependency intake. Redistributable artifacts include
   SDL-family zlib notices and notices required by dependencies embedded in
   the selected native image/font packages.
9. Native loading, AOT, activity/main integration, and rendering are verified
   independently per target. Source-generated interop and package layout are
   favorable signals, not evidence that an unexecuted mobile build works.

## Alternatives considered

### Handwritten `LibraryImport` layer

Rejected for the compatibility milestone. It would reduce wrapper surface but
transfer ABI generation, platform bootstrap, mobile linkage, and companion
library packaging to Velora before the rendering contract is stable. A future
vendored fork of the accepted immutable binding is preferable if upstream
maintenance stops.

### `ppy/SDL3-CS`

Kept as the primary fallback. It is mature and permissively licensed, but the
evaluated package is more monolithic, uses conventional `DllImport`, and did
not provide an equivalent verified iOS `buildTransitive` linkage path.

### `bottlenoselabs/SDL3-cs`

Rejected for this architecture because its current support matrix excludes
Android and iOS.

### `flibitijibibo/SDL3-CS`, Silk.NET.SDL, and Sdl3Sharp

Rejected for the compatibility milestone because they respectively require
substantially more native/mobile packaging ownership, still target SDL2, or
remain too early and incomplete for the required platform matrix.

## Consequences

- Velora receives source-generated SDL3 interop and a verified desktop/mobile
  native package family without leaking the wrapper into domain code.
- One upstream maintainer and native package supply chain remain risks. Exact
  pins, hashes, notices, a private adapter, and the ability to vendor the
  immutable generator output mitigate them.
- Linux native packages currently require glibc 2.28 or newer; release support
  claims must reflect the package actually shipped.
- iOS static libraries increase artifact size and require host-level linkage.
  SDL GPU/Metal cannot be validated in the iOS Simulator and therefore needs
  device evidence.
- Android renderer/GPU capability varies by device. The SDL 2D path remains
  the required fallback and device-matrix results must be reported separately.
- The current machine has no .NET Android/iOS workloads. This ADR authorizes
  implementation and compile-smoke infrastructure but does not claim local
  mobile compile, link, AOT, simulator, signing, or device success.

## Verification references

- [SDL3-CS release v3.4.14.1](https://github.com/edwardgushchin/SDL3-CS/releases/tag/v3.4.14.1)
- [SDL3-CS supported platforms](https://github.com/edwardgushchin/SDL3-CS#supported-platforms)
- [SDL GPU documentation](https://wiki.libsdl.org/SDL3/CategoryGPU)
- [SDL Android integration](https://wiki.libsdl.org/SDL3/README-android)
- [SDL iOS integration](https://wiki.libsdl.org/SDL3/README-ios)
- [SDL_image stream loading](https://wiki.libsdl.org/SDL3_image/IMG_LoadTexture_IO)
- [SDL_ttf IO APIs](https://wiki.libsdl.org/SDL3_ttf/QuickReference)
