# Velora Engine — Migration Architecture

Status: **Approved baseline** (Phase 0). This document records the locked target
architecture for the C++20 → C#/.NET migration. Changes require an ADR.

Companion documents:

- [COMPATIBILITY.md](COMPATIBILITY.md) — source-verified behavior contract of the C++ oracle
- [ROADMAP.md](ROADMAP.md) — phased execution plan and gates
- [STATUS.md](STATUS.md) — persistent cross-session handoff state
- [../adr/](../adr/) — Architecture Decision Records

---

## 1. Mission and Non-Goals

Velora is a cross-platform visual-novel engine and authoring platform.
The migration replaces the compact C++20 runtime with a modular C#/.NET
architecture and evolves it into an authoring platform (VelScript language
tooling, headless runtime, Player, Avalonia Editor with code / live-preview /
visual authoring modes, packaging, plugins).

Non-goals (permanent unless an ADR changes direction):

- Velora is **not** a general-purpose game engine. 2D visual novels are the
  product.
- **2D is the mandatory and default rendering model.** 2.5D and optional,
  narrowly scoped 3D are post-parity work (ROADMAP Phase 22+) and must never
  change the default authoring model.
- No arbitrary C# embedding in VelScript (AGENTS.md §13).

## 2. Target Platform Matrix

| Tier | Platforms | Components | Status |
|------|-----------|------------|--------|
| 1 | Windows, Linux, macOS | Runtime, Player, Editor (Avalonia), CLI tools | First-class; parity milestone target |
| 2 | Android, iOS | Player only (no Editor) | Post-parity (ROADMAP Phase 23). Architecture-ready now; **not implemented** during initial parity |
| Desktop-only | PreviewHost, CLI, locale extraction, Editor itself | — | By design |

Platform rules:

- Platform-neutral libraries target plain `net10.0`. No multi-targeting of
  pure libraries for portability testing; dedicated mobile compile-smoke host
  projects verify neutrality (Phase 1).
- All platform contact points go through interfaces (see §8). No
  `Environment.*`, wall-clock, console, or filesystem assumptions inside
  Runtime.
- Mobile constraints honored by construction: AOT-friendly code (no
  Reflection.Emit, no dynamic codegen in neutral layers), suspend/resume
  lifecycle, touch-first input mapping, safe areas/orientation as presentation
  concerns, app-container storage, audio interruption handling, DPI/scale
  factor in surface descriptors.

## 3. Technology Direction

| Concern | Choice |
|---------|--------|
| Language | C# |
| Runtime | .NET 10 LTS |
| Nullable reference types | enabled, warnings as errors |
| Editor UI | Avalonia — **Editor only** |
| Runtime windowing/input/rendering backend | SDL3 (C# bindings; binding choice ADR at Phase 10) |
| Graphics | 2D-first; SDL3 GPU (Metal on macOS/iOS, Vulkan on Win/Linux/compatible Android) is the advanced path (Phase 22); fallback rendering remains a capability flag, no GLES/software backend promised until evaluated |
| Math | System.Numerics where appropriate |
| JSON/config | System.Text.Json where appropriate |
| Tests | xUnit (+ golden fixtures) |
| Benchmarks | BenchmarkDotNet where useful |
| Player packaging | self-contained .NET deployment |
| NativeAOT | optional optimization; mobile targets are AOT-compatible by construction |

Dependency additions require the checklist in AGENTS.md §42 and an ADR for
major choices.

## 4. Language Pipeline (VelScript)

```text
SourceText (.vel, UTF-8 on disk)
    → Lexer (ReadOnlySpan<char>)
    → Lossless full-fidelity syntax tree (tokens + trivia + spans + diagnostics)
    → Semantic Model (symbols, binding, diagnostics)
    → Compiler
    → Velora IR (instruction list + source map)
    → Headless Runtime
```

Locked properties:

- **Full fidelity is mandatory; incremental green/red trees are not.** The
  tree must reproduce the source exactly (`ToFullString()` round-trip) and
  survive edits without losing comments/formatting. Roslyn-style green/red
  nodes and an incremental parser are an *optional later optimization* behind
  the same public API, introduced only if Editor performance justifies it
  (evaluated Phase 14+).
- **Diagnostics, not exceptions.** Parsing malformed source produces a tree
  with error nodes plus a diagnostic list. Malformed input is a normal Editor
  condition.
- **UTF-8 files / UTF-16 spans.** `.vel` files are UTF-8 on disk (policy on
  BOM: tolerated on read, not written). In memory, `SourceText` is a .NET
  string and every `TextSpan` offset/length is in **UTF-16 code units** for
  editor interoperability. The C++ oracle counted **bytes** for lexer columns;
  the divergence is documented in COMPATIBILITY.md (line numbers agree;
  columns differ on non-ASCII lines). Golden tests compare line numbers
  exactly and columns only on ASCII fixtures.
- **Code-point semantics.** Typewriter reveal and any character-level runtime
  behavior operate on Unicode code points via `System.Text.Rune`, matching the
  C++ UTF-8 decoder's behavior (including U+FFFD substitution for malformed
  sequences). Grapheme-cluster handling is *not* current behavior and may only
  be added later as an intentional, documented language/runtime change.
- **Semantic analysis owns resolution.** Labels, characters, screens, and
  variables become symbols at the semantic phase. Runtime leniency (e.g.,
  unknown jump target logs and continues) is a *runtime* policy preserved for
  compatibility, not a reason to skip semantic diagnostics.
- **Source map.** Every IR instruction carries a source `TextSpan` and its
  containing label. See §10 for identity semantics.

## 5. Runtime

The Runtime is **headless and deterministic**:

- No window, GPU, audio device, filesystem, clock, or RNG dependency.
- Deterministic given (compiled program, initial state, scripted inputs,
  `Update(TimeSpan)` deltas). Presentation randomness (shake, particles,
  glitch) lives in backends and is seedable.
- Execution model: `Start`, `Advance`, `SelectChoice`, `SubmitTextInput`,
  `Update(delta)`; full state is serializable (`RuntimeState`), including
  in-flight waits and active tweens (tween ownership moved from the renderer
  into runtime state — eliminating the C++ `60×update(0.1)` restore hack).

### Services vs Events vs Lifecycle (locked boundary)

1. **Synchronous services** are *injected* and queried by the runtime:
   `IAssetResolver`, and `IPlatformInfo` (user name, preferred locale, and
   local-time text for the `os_*` builtins). The runtime has no ambient/system
   wall-clock dependency; **all** time-derived behavior flows through the
   injected platform/time abstraction (`IPlatformInfo` covers time-text needs;
   if a future need requires a different time role, it gets its own clearly
   named service rather than overloading this one). Deterministic override
   hooks on these services keep tests/replay reproducible. The runtime never
   calls upward into presentation.
2. **RuntimeEvents are one-way** records emitted by the runtime:
   `DialogueStarted`, `BackgroundChanged`, `SpriteShown/Hidden`,
   `SpritePropertyChanged`, `ChoiceRequested`, `TextInputRequested`,
   `AudioRequested/Stopped`, `EffectRequested`, `VariableChanged`,
   `ExecutionPositionChanged`, `TitleChanged`, `SaveLockChanged`,
   `QuitRequested`, `MainMenuRequested`, warning events for lenient-error
   paths. Hosts consume; they do not answer.
3. **Lifecycle originates from platform hosts** (and Player.Core):
   `Suspend/Resume/LowMemory` flow *downward* into explicit
   `CaptureState()/Restore()` calls on the runtime. The runtime exposes no
   lifecycle callbacks of its own. `IRuntimeHost` as a catch-all God Object
   is forbidden.

### Runtime.Contracts

`Velora.Runtime.Contracts` holds the runtime's public data vocabulary:
event/state/intent records, `SceneState`/`DialogueState`/`VariableStore`
shapes, `SaveKind`, `ProgramFingerprint`. Runtime and all consumers depend on
it; it depends on nothing.

## 6. Editor / PreviewHost Process Boundary (locked)

- **Velora.Editor** owns language/project *authoring*. Its dependency list is
  exactly: `Editor.Protocol`, `Script.Syntax`, `Script.Semantics`,
  `Script.Compiler`, `Runtime.Contracts`, `ProjectModel`, `Assets`,
  `Localization`. It has **no** direct Runtime, Persistence, Rendering, or
  Audio execution dependency.
- **Velora.PreviewHost** is a separate process and the *sole* owner of actual
  preview execution: it runs the real `StoryRuntime` with real backends.
  Crash isolation + runtime parity are the reasons it exists.
- Communication: control messages over local IPC (JSON contracts in
  `Editor.Protocol`); rendered frames over a binary transport (shared-memory
  ring buffer preferred, length-prefixed raw BGRA over local socket as
  fallback). Pixel frames are never base64/JSON/stdout at interactive rates.
- Preview uses the **same runtime semantics** as the Player — there is no
  second, fake preview runtime.
- If a future in-process runtime use case becomes genuinely necessary, it
  must be introduced deliberately via ADR, not absorbed into the Editor's
  default graph.
- `PreviewAtSource` pipeline: caret offset → syntax/semantic node →
  source-mapped IR instruction → nearest checkpoint (captured at
  Say/Choice/label entry) → deterministic replay with an explicit route
  context (choices default to index 0 for compatibility, overridable) →
  preview frame + state.

## 7. Game UI vs Editor UI (locked)

- **Avalonia is Editor UI only.**
- The actual visual-novel UI — dialogue box, choices, main menu, save/load,
  settings, backlog, quickbar, HUD, and project-defined screens — is a
  **platform-neutral model in `Velora.UI`**, rendered by the **same Velora
  rendering stack** used by Player and PreviewHost.
- Rationale: exact preview parity (what you see in the Editor preview is what
  players see), full UI customization/theming later, and trivial
  Windows/Linux/macOS/Android/iOS export of a finished novel.
- The C++ Engine's procedurally drawn menus/backlog/quickbar/text-input card
  become `Velora.UI` model data plus one renderer path. Parity goldens cover
  game UI as well as scene content.
- `Velora.UI` absorbs the current `ScreenElement` system (panel/text/
  imagebutton) as the project-defined subset of the same model.

## 8. Platform Abstractions (scope locked)

`Velora.Platform.Abstractions` is **services-only**. It contains exactly:

- `IPlatformInfo` (user name, preferred locale, local time text)
- `ISaveStorage` (save slot open/read/write/delete/list)
- `IScriptDataStore` (the `meta file_write/file_delete` sandbox)
- application/lifecycle platform services (suspend/resume/low-memory sink)

Explicitly **not** here (owned by `Velora.Rendering.Abstractions`):
graphics surface descriptors, scale factor, safe-area insets, orientation,
graphics capabilities/fallback flags.

PreviewHost frame transport is not a platform abstraction; it belongs to the
preview boundary (§6).

Implementations live in hosts: `Player.Desktop` (and later
`Player.Android/.iOS`), CLI, tests (in-memory fakes).

## 9. Persistence

- Save container: `VSAV` magic + uint16 little-endian version + CBOR payload;
  persistent store: `VPER`. Slots are semantic `SaveKind`:
  - `Manual(int slot)` — legacy numeric slots **0..20 preserved**;
  - `Quick` — maps to slot 0 (legacy behavior);
  - `Auto` — rotating autosave history, **depth 3**, internal persistence/
    player policy; never consumes or alters manual slots. Mobile suspend
    triggers an autosave; desktop focus loss alone does not.
- **Compatibility contract (ADR-0006):** semantic/interoperable VSAV v2
  compatibility. C# reads real C++ v2 files; C# writes v2 the current C++
  can read **only during the migration window** (through the C# Player
  parity / C++ legacy-retirement milestone). After retirement, C# is the
  authoritative writer; newer C# versions keep reading supported historical
  saves per policy. Byte-for-byte CBOR equality is *not* a requirement
  unless the empirical interop test proves it necessary.
- Legacy readers are permanent: `SaveFileReaderV1` (binary, host-endian) and
  v2 reader stay as version-specific readers. Legacy `.vsav` files are
  regression fixtures.
- All serialization in neutral layers is reflection-free (AOT/iOS-safe).
- Validation limits from the C++ oracle (16 MiB cap, ≤10 000 variables,
  ≤1 000 sprites, ≤128 audio entries, slot bounds) are preserved as
  untrusted-input defenses.

## 10. Instruction Identity (semantics locked)

Ownership and the compiler dependency are fixed by
[ADR-0011](../adr/ADR-0011-shared-execution-identity.md).

- **`InstructionId`** = identity *within one compiled `VeloraProgram`*
  (index/ordinal). It is **not** stable across recompilation and must never
  be persisted as a cross-edit identifier.
- **`ProgramFingerprint`** = content hash over the compiled IR + compiler
  version, persisted with saves. Save resume resolves: fingerprint match →
  restore by instruction; mismatch → fallback by label name + nearest
  statement, with a user-facing "save may resume at a slightly different
  position" policy. The current v2 `script_line` semantics remain the
  parity-era contract; fingerprint-based resolution is the future evolution
  path (ADR when the save format evolves).

## 11. Plugins (future direction, not implemented now)

Two capability tiers are the *documented direction* (ADR-0008 at Phase 21):

- **Tier A — portable/AOT**: statically registered, metadata-driven
  extensions (commands, transitions, effects, validators, importers, panels).
  Works on all Tier 1 and Tier 2 platforms.
- **Tier B — desktop/editor dynamic**: dynamically loaded plugins via an
  appropriate isolated loading mechanism (mechanism chosen at Phase 21).
  Export tooling reports when a project's plugin set is not mobile-compatible.

No plugin runtime is built before the underlying boundaries stabilize.

## 12. Project Structure

```text
src/
    Velora.Platform.Abstractions/   services-only (§8)
    Velora.Platform.Desktop/        contained desktop storage/host services
    Velora.ProjectModel/            minimal .velproj record
    Velora.Script.Syntax/           SourceText, TextSpan, tokens, trivia,
                                    lossless tree, parser, diagnostics
    Velora.Script.Semantics/        symbols, binding, diagnostics
    Velora.Script.Compiler/         semantic model → IR + source map
    Velora.Runtime.Contracts/       events/state/intent records, SaveKind,
                                    ProgramFingerprint
    Velora.Runtime/                 headless deterministic execution
    Velora.Assets/                  logical paths, mounts, containment policy
    Velora.Localization/            locale catalogs and translation
    Velora.Localization.Tooling/    source extraction and catalog validation
    Velora.Persistence/             VSAV/VPER readers+writers (v1, v2, ...)
    Velora.Text/                    rich text and deterministic typewriter
    Velora.UI/                      platform-neutral game UI model
    Velora.Audio/                   deterministic mixer state + backend contract
    Velora.Audio.Miniaudio/         first-party native miniaudio adapter
    Velora.Rendering.Abstractions/  surface descriptors, scale/safe-area/
                                    orientation, device capabilities
    Velora.Rendering.Sdl/           SDL3 backend implementation
    Velora.Player.Core/             shared player shell (state machine,
                                    input→command mapping, autosave policy)
    Velora.Player.Desktop/          Windows/Linux/macOS host
    Velora.Cli/                     headless runner/tracer/compile/locale tools
    Velora.PreviewHost/             preview execution process (§6)
    Velora.Editor.Protocol/         control IPC contracts
    Velora.Editor/                  Avalonia authoring application
    (Velora.Player.Android/.iOS     post-parity targets, Phase 23)
tests/
    Velora.Script.Tests/  Velora.Runtime.Tests/  Velora.Persistence.Tests/
    Velora.Compatibility.Tests/  (+ KitchenSink fixtures under samples/)
samples/
    KitchenSink/           exercises every supported VelScript feature
```

## 13. Dependency Direction (locked graph)

```text
Platform.Abstractions   [none]
Platform.Desktop      → Platform.Abstractions
Script.Syntax           [none]
Script.Semantics      → Script.Syntax
Script.Compiler       → Script.Semantics, Script.Syntax, Runtime.Contracts
Runtime.Contracts       [none]
Runtime               → Script.Compiler, Runtime.Contracts,
                        Assets(ifaces), Localization(ifaces),
                        Platform.Abstractions(ifaces)
Assets                 [none]      Localization [none]   ProjectModel [minimal]
Persistence          → Runtime.Contracts
Rendering.Abstractions [self-contained contract]
UI                   → Rendering.Abstractions
Audio                  [self-contained contract]
Audio.Miniaudio      → Audio
Rendering.Sdl         → Rendering.Abstractions, UI, Assets
Player.Core          → Runtime, Persistence, UI, Platform.Abstractions
Player.Desktop       → Player.Core, Rendering.Sdl, Audio.Miniaudio,
                       Platform.Abstractions
Cli                  → Script.Compiler, Runtime, Persistence, Localization
Editor.Protocol        [none]
PreviewHost          → Editor.Protocol, Runtime, Persistence,
                       Rendering.Sdl, Audio.Miniaudio, Assets, ProjectModel
Editor               → Editor.Protocol, Script.Syntax, Script.Semantics,
                       Script.Compiler, Runtime.Contracts, ProjectModel,
                       Assets, Localization
```

Forbidden (AGENTS.md §7, enforced): Script→SDL/Avalonia; Runtime→backends;
Runtime/Core→Editor; Editor→Runtime/Persistence/Rendering/Audio execution;
parser→concrete renderer; a second preview runtime.

## 14. Rendering Direction

The binding and native-package boundary is fixed by
[ADR-0003](../adr/ADR-0003-rendering-backend.md): `SDL3-CS` is private to the
SDL adapter, while platform-specific native packages belong to concrete hosts.

1. **Parity first (Phase 10–11):** reproduce C++ scene behavior on SDL3 2D
   API: 1280×720 logical letterbox, texture caching (incl. negative cache),
   background crossfade (all named transitions), sprite layout law
   (height = 80% of viewport, bottom-anchored, x-centered, z-order sorted,
   rotation/flip), procedural fallback background, flash/shake, procedural
   post effects (blur via quarter-res ping-pong, vignette rings, CRT
   scanlines, glitch strips, fade, color grade) with seeded RNG.
2. **Modern GPU (Phase 22):** SDL3 GPU pipeline, real post-processing
   shaders, Metal/Vulkan paths; the 2D backend remains a valid fallback via
   `IGraphicsDevice.Capabilities`. No GLES/software backend promised until
   evaluated (ADR).
3. Per-frame allocation discipline: cached glyph atlases/textures replace
   the C++ surface-per-text-draw pattern; measured, not speculative.

## 15. Audio Direction

The native boundary is fixed by
[ADR-0009](../adr/ADR-0009-audio-backend.md). `Velora.Audio` owns deterministic
mix state and the device-neutral backend contract. `Velora.Audio.Miniaudio`
is a first-party narrow adapter over a vendored, exact miniaudio revision; raw
native structs and real-time callbacks never cross into managed code. Player
hosts own Android AudioFocus / Apple AVAudioSession policy. Ogg support remains
gated on reviewed native decoder pins and sanitizer/fuzz evidence.

## 16. Security

- VelScript is untrusted. Script filesystem access is confined to
  `IScriptDataStore` with **canonical containment** (ADR-0010): resolve/
  canonicalize the candidate; reject absolute paths and anything whose final
  resolved path escapes the canonical allowed root; equivalent containment
  for mounted roots. This is an intentional hardening relative to the C++
  oracle where behavior differs (see COMPATIBILITY.md §Deviations — benign
  intra-root `..` is accepted by both; symlink-escape edge cases are closed
  in C#).
- No arbitrary or unrestricted script-driven process execution, network
  access, or dynamic code execution. Any future controlled network capability
  requires an explicitly sandboxed API, a security review, and an ADR before
  implementation.
- Save files are untrusted input: size/count/schema limits enforced.
- Plugins (future) are trusted code, clearly separated from the VelScript
  sandbox; Tier B dynamic plugins load in isolation.
- PreviewHost runs with stricter roots (project dir only) and no user-data
  writes beyond its own IPC channel.

## 17. Testing and Verification Strategy

- Lexer golden tests (token streams incl. `example.vel`, escapes, colors,
  comment forms); line numbers compared exactly, columns on ASCII fixtures.
- Parser tests per statement + malformed-input diagnostic tests.
- Round-trip tests: `ToFullString() == source` over the corpus.
- Semantic tests: label/character/screen resolution, duplicate labels,
  unknown targets.
- **Runtime trace tests:** headless stories produce ordered event traces;
  golden traces generated from instrumented C++ runs (Phase 0/2 tooling)
  over the KitchenSink corpus.
- **Persistence:** golden `.vsav`/`.vsav`-persistent binaries generated by
  the C++ oracle; corruption matrix (magic/version/truncation/oversize/
  type-tags); slot bounds. v1 coverage per fixture-hunt outcome (see
  STATUS.md).
- Rendering goldens: offscreen deterministic frames, seeded RNG,
  tolerance-based pixel comparison.
- Editor round-trip tests: visual mutation → minimal text edit → reparse
  equivalence.
- KitchenSink (`samples/KitchenSink/`) exercises every documented VelScript
  capability (docs/velscript.md command reference) and feeds trace/golden
  suites.

## 18. Performance Policy

Simple, correct implementations first (`ReadOnlySpan<char>` lexer, plain
record/class IR). No speculative pooling/`ReadOnlySequence` machinery.
Benchmark gates (lexer throughput, parse latency, update µs/frame, editor
typing latency) introduced at Phase 5 and enforced before optimization work.
Frame-critical paths avoid per-frame allocations (glyph atlases, cached
textures) — this is a known C++ hotspot being fixed, not a speculative one.

## 19. Definition of Done (per phase)

A phase is complete when: scope matches ROADMAP; architecture rules hold;
code builds; relevant tests pass; compatibility verified against the oracle;
no unrelated regressions; no debug scaffolding remains; STATUS.md updated;
diff reviewed. The hard gate stands: **no Editor implementation and no
advanced graphics before C# Player parity (Phase 12) without explicit
approval.**
