# Velora Migration Status

Last updated: **2026-10-28**

This is the persistent handoff for the active C++20 → C#/.NET rewrite. It
records working-tree facts and executed verification only; roadmap targets are
not implementation claims.

## Current phase

**Phases 0 through 5 are complete.** Phase 6 has a working deterministic
headless runtime, capture/restore, and CLI simulation. Its remaining edge-case
compatibility audit is active; Player parity is not complete.
Independent, tested foundations now also exist for persistence compatibility,
project/assets, localization, audio intent, rendering contracts, and platform
services. These foundations do not imply that the corresponding later Player
parity phases have exited.

The hard sequence remains: C# Desktop Player parity (Phase 12) before Editor
execution features or advanced graphics. Android/iOS Player/export remains
required in this same run at Phase 23; it is sequenced after parity, not dropped
or converted into a documentation-only future task.

## Completed and source-verified work

- The repository contract (`AGENTS.md`) and approved target architecture
  (`ARCHITECTURE.md`) are present.
- The C++ language, runtime, persistence, assets/path security, localization,
  audio, rendering, UI/settings, build, and host toolchains have been audited.
- [COMPATIBILITY.md](COMPATIBILITY.md) records source-verified parity,
  intentional hardenings, known oracle bugs, exact VSAV/VPER contracts, and
  unresolved questions.
- [ROADMAP.md](ROADMAP.md) defines implementation/verification gates for the
  continuous run.
- Initial accepted decisions exist for the [.NET foundation](../adr/ADR-0001-dotnet-runtime-foundation.md),
  [lossless language pipeline](../adr/ADR-0002-lossless-language-pipeline.md),
  [SDL3 rendering backend](../adr/ADR-0003-rendering-backend.md),
  [first-party miniaudio boundary](../adr/ADR-0009-audio-backend.md),
  [save compatibility](../adr/ADR-0006-save-compatibility.md),
  [canonical path containment](../adr/ADR-0010-canonical-path-containment.md),
  and the [shared compiler/runtime identity boundary](../adr/ADR-0011-shared-execution-identity.md).
- Authentic C++ v2 VSAV/VPER fixtures and an explicitly synthetic,
  host-native v1 contract fixture are present under
  `tests/fixtures/persistence/`. `tests/LegacyFixtureGenerator.cpp` provides
  generation/read-back verification without pretending the v1 file is a
  recovered historical save.
- The updated C++ oracle configuration completes **4/4** tests, including the
  persistence fixture verifier and production-VM runtime oracle trace.
- The Phase 2 lexical layer is implemented and reviewed: immutable UTF-16
  source text and line mapping, lossless tokens/trivia, stable lexer
  diagnostics, BOM-tolerant UTF-8 input, legacy comments/escapes/numbers/
  colors, malformed-input recovery, and exact full-string reconstruction.
- The Phase 3 parser is implemented and reviewed: a public immutable typed AST
  covers the complete legacy grammar, including screens and nested blocks;
  valid source and malformed partial source round-trip exactly; missing tokens
  plus stable `PAR001`–`PAR005` diagnostics permit recovery without exceptions.
- The Phase 4 semantic layer is implemented and reviewed: symbols and
  deterministic indexes cover labels, characters, screens, and variables;
  duplicate and unresolved references remain compatible while producing
  diagnostics; reference/rename queries preserve UTF-16 source spans.
- The Phase 5 compiler is implemented and independently reviewed: immutable
  syntax-independent IR covers every legacy statement family; dense IDs,
  UTF-16 source maps, semantic-model-owned target resolution, explicit CFG
  continuations, startup preamble, stable canonical tags/fingerprints, and
  recoverable diagnostics are locked by 34 focused tests. Legacy optional
  defaults are preserved. Empty menus are rejected as IH-008 instead of
  producing the oracle's unrecoverable zero-choice wait.
- `tests/LegacyRuntimeOracle.cpp` executes the production C++ `ScriptVM` and
  verifies a committed deterministic trace for startup, mixed coercion,
  duplicate/missing flow targets, choice/text input, and the nested-pause bug.
  CQ-001 is resolved as IH-007: C# will use correct explicit continuations.
- `Velora.Runtime.Contracts` and `Velora.Persistence` read bounded VSAV v1/v2
  and VPER v2 input, write interoperable v2 containers, reject corrupt or
  non-finite input, and expose semantic manual/quick/auto save targets. Quick
  save maps to legacy slot 0; autosaves rotate separately at depth 3.
- Cross-language persistence is proven in both directions: committed C++ v2
  fixtures are read by C#, and C#-written v2 VSAV/VPER files pass the production
  C++ fixture verifier.
- `Velora.ProjectModel` loads a bounded strict version-1 `.velproj` schema.
  `Velora.Assets` provides ordinal logical IDs, mounted roots, full canonical
  symlink containment (including missing leaves), exact-case diagnostics, and
  a deterministic asset database.
- `samples/KitchenSink` is a strict version-1 project with deterministic SVG
  assets and one source file that exercises every compatibility-era statement
  family. Syntax, compiler/continuation, project/assets, and localization
  tooling tests consume the same fixture.
- Platform-neutral localization/extraction, audio-state, rendering/surface,
  rich-text/typewriter, game UI, runtime-state contracts, and platform service
  foundations are implemented with focused tests. The BCL-only desktop adapter
  adds contained atomic save/script-data storage and deterministic host values.
  The first SDL3 2D adapter slice builds with exact managed package pinning and
  explicit native ownership; its native smoke remains skipped without a native
  host package. Main-thread checks, bounded caching, fallback/transition state,
  readback validation, and exceptional initialization cleanup are implemented.
- `Velora.Runtime` executes the real compiler IR, including nested pauses,
  choices/input/waits, variables, dialogue, sprite/tween/emotion state, screen
  and particle intents, audio requests, and explicit host-operation events.
  Restores retain executed definitions across backward jumps and do not replay
  host side effects. Both KitchenSink routes finish deterministically.
- `Velora.Cli` validates `.vel`/`.velproj` input and simulates stories with the
  production Runtime. It bounds script reads, resolves manifest entry paths
  through canonical containment, reports source diagnostics, and limits loops.
  Simulation suppresses external host operations. It is not a visual Player.
- `Velora.Audio.Miniaudio` and its native C shim build and pass real WAV
  open/duration/seek/cursor/suspend/resume/teardown tests with no audio device.
  A Unicode filename crosses the actual managed/native boundary. Independent
  `native/audio` CMake builds pin and hash-check the miniaudio header. This does
  not yet prove audible playback, Ogg support, or Player mixer integration.

## Completed .NET foundation

- `global.json` pins SDK `10.0.403` with patch-only roll-forward.
- `Directory.Build.props` enables nullable analysis, implicit usings,
  warnings-as-errors, deterministic builds, and recommended .NET analyzers.
- `Velora.sln` contains the implemented language, contracts, persistence,
  project/assets, localization/tooling, audio, rendering, text, game UI,
  runtime, CLI, native-audio adapter, compiler, SDL adapter, and platform libraries plus their
  focused test projects;
  repository ignore/editor settings cover .NET output.
- The test project uses `xunit.v3.mtp-v2` 4.0.0 and Microsoft Testing Platform,
  without the older xUnit v2/VSTest adapter stack.
- `.github/workflows/ci.yml` defines locked restore/build/test on
  Ubuntu 24.04, Windows 2025, and macOS 15 plus an Ubuntu C++ oracle job. Its
  native-audio C/managed tests and CLI sample/publication checks are included.
  Only an actual GitHub run can supply runner evidence.

The pinned SDK successfully restores the locked packages and builds the current
solution with zero warnings and errors.

## In-progress implementation

### Phase 6 deterministic runtime

The runtime now passes these gates for the tested corpus. Mixed-coercion and
lenient-flow tests read expected variables from the committed production C++
oracle JSON. Numeric-overflow/prefix regressions, repeated particle emitters,
definition restoration, and single-owner interpolation have focused tests.
Broader numerical corner cases (including hexadecimal strings accepted by
`std::stod`) and presentation-host integration remain unverified.

### SDL parity corrections

Independent review confirmed the binding ABI, IOStream lifetime, BGRA/pitch
copy, and ownership order, and identified missing fallback/crossfade state,
SDL main-thread enforcement, explicit readback format, bounded decoded texture
cache, and exceptional-init cleanup. These corrections now pass managed tests;
real native rendering and host packaging still gate Phase 10.

### Later-phase foundations

The persistence storage policy, project/assets boundary, localization catalog
and extraction, audio intent model, rendering surface/state abstractions,
rich-text/typewriter, game UI, runtime snapshot contracts, and platform service
interfaces are usable and tested. Runtime behavior, native backends, Player
lifecycle integration, and complete save/replay orchestration remain ordered
work rather than implied completion.

### Phase 0 fixture update

`CMakeLists.txt` defines both `VeloraLegacyFixtureGenerator` and
`VeloraLegacyRuntimeOracle`. Release reconfiguration, target build, and the
complete four-test CTest suite succeeded locally.

## Toolchain and platform facts

| Target | Locally available evidence | Current limitation |
| --- | --- | --- |
| macOS | Apple Silicon host. Neutral C# build/tests, real native audio interop, and self-contained CLI publication run locally. | No visual C# Player/Editor artifact exists yet. |
| .NET | Official SDK 10.0.403/runtime 10.0.14 at `/private/tmp/velora-dotnet-10.0.403/dotnet`; SDK archive SHA-512 verified against official release metadata. | Isolated SDK; optional/mobile workloads remain unavailable. |
| iOS | Xcode 26.6, iOS SDK 26.5, and simulator tooling are available when commands use scoped `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`. | .NET Apple workloads, signing identity, provisioning, device validation, and a mobile host do not yet exist. Do not claim an iOS build. |
| Android | No usable Android SDK, JDK, Gradle installation, .NET Android workload, or workload cache was found locally. | Local Android compile/package validation is externally unavailable until tooling is installed. Host/export implementation and CI validation remain required. |
| Windows/Linux | Self-contained CLI cross-publication succeeds for `win-x64` and `linux-x64`. Native audio and managed test jobs are defined for both. | No actual Windows/Linux execution evidence; CI must supply it. |

The missing Android/iOS tooling blocks only local mobile compilation/signing,
not neutral implementation, exporters, validation, or CI work.

## Locked decisions and compatibility alerts

- The C++ implementation remains the semantic oracle and must not be deleted.
- Language source is lossless UTF-8-on-disk / UTF-16-spans-in-memory; Runtime
  executes source-mapped IR, not syntax nodes.
- Runtime is headless/deterministic; Player, tests, PreviewHost, debugger, and
  CLI share it.
- C# must read VSAV v1/v2 and VPER v2. C# v2 writes must remain interoperable
  with the C++ reader through the legacy-retirement milestone.
- Asset/script-data resolution uses canonical final-target containment. Closing
  symlink/mount escapes is intentional hardening.
- Duplicate labels currently last-win, missing jumps warn and continue, and
  mixed expressions use legacy coercion. Preserve these through parity with
  diagnostics.
- The C++ VM incorrectly ignores pauses inside `if`/choice bodies and overwrites
  a selected choice's waiting state. This bug is recorded, not approved as the
  new continuation model.
- `docs/velscript.md` says `config text_speed` and `config game_locale` affect
  runtime behavior, but the C++ engine only consumes `ui_accent_color`. Do not
  silently choose documentation or implementation without the focused
  compatibility decision in CQ-004.

## Verification ledger

| Date | Command/check | Result |
| --- | --- | --- |
| 2026-10-28 | SDK 10.0.403 locked restore and Debug solution build | **Passed**, 37 projects, 0 warnings/errors. |
| 2026-10-28 | All 18 generated test runners, real native audio enabled | **465 passed, 1 skipped, 0 failed** (466 total). SDL native smoke skipped because SDL3 is unavailable to the runner. Runtime 15/15; native-audio adapter 13/13; CLI 6/6. |
| 2026-10-28 | Standalone `native/audio` Release build and CTest | **Passed 1/1**, generated WAV with no device. Managed native interop additionally covers a Unicode path. |
| 2026-10-28 | Self-contained CLI publication | **Passed** for macOS arm64, Windows x64 and Linux x64. File inspection confirms Mach-O, PE32+ and ELF architecture respectively; only macOS execution is locally tested. |
| 2026-10-28 | Published macOS CLI validation and KitchenSink simulation | **Passed**, both showcase and alternate routes finish, with host side effects suppressed. |
| 2026-10-28 | CI YAML syntax and tracked diff whitespace | **Passed** locally; GitHub runner execution is still pending. |
| 2026-08-22 | Existing C++ CTest configuration before fixture-generator addition | **Passed 2/2**: `VeloraCoreTests`, `VeloraVersion`. |
| 2026-08-22 | Source-level language/systems/toolchain audit | Completed; findings captured in architecture/compatibility/ADRs. |
| 2026-08-22 | `dotnet restore Velora.sln --locked-mode` (SDK 10.0.400, single MSBuild node) | **Passed.** Both current projects restored from checked lock files. |
| 2026-08-22 | `dotnet build Velora.sln --configuration Debug --no-restore` (single MSBuild node) | **Passed**, 0 warnings, 0 errors. Parallel MSBuild nodes are unavailable in the managed sandbox, so local verification uses `/m:1`. |
| 2026-08-22 | `.NET 10 MTP: dotnet test --project tests/Velora.Script.Tests/Velora.Script.Tests.csproj --no-build --minimum-expected-tests 77` | **Passed 77/77.** The MTP named pipe requires execution outside the filesystem/process sandbox; the standalone generated runner also passes 77/77 inside it. |
| 2026-08-22 | Release CMake reconfigure/build plus complete CTest | **Passed 3/3**: `VeloraCoreTests`, `VeloraLegacyPersistenceFixtures`, `VeloraVersion`. |
| 2026-08-22 | `.github/workflows/ci.yml` local YAML parse and diff check | **Passed.** Workflow schema/runner execution remains unclaimed until GitHub executes it. |
| 2026-08-22 | Phase 3 parser focused build and standalone xUnit runner | **Passed 93/93**, 0 warnings, 0 errors. Bundled script and full grammar round-trip exactly; malformed fixtures recover. |
| 2026-08-22 | C++→C# and C#→C++ persistence interop | **Passed.** Authentic generated v2 VSAV/VPER fixtures load in C#; C# writer output passes `VeloraLegacyFixtureGenerator --verify`. Synthetic v1 remains explicitly labelled. |
| 2026-08-22 | Integrated `Velora.sln` locked restore/build and standalone focused runners | **Passed**, 0 warnings, 0 errors, **222/222 tests** across Script (93), Localization (20), Audio (12), Rendering (17), Platform (13), Persistence (24), and Assets/ProjectModel (43). |
| 2026-08-22 | Expanded integrated `Velora.sln` locked restore/build and all standalone focused runners | **Passed**, 0 warnings, 0 errors, **350/350 tests** across Syntax (93), Semantics (22), Localization (20), Localization Tooling (5), Audio (14), Rendering (17), Platform Contracts (13), Desktop Platform (28), Runtime Contracts (21), Persistence (24), Assets/ProjectModel (43), Text (9), and UI (41). |
| 2026-08-22 | Release C++ production-VM oracle trace and complete CTest | **Passed 4/4**. `legacy-runtime-contract.json` is reproduced exactly by the current production `ScriptVM`. |
| 2026-08-22 | Phase 5 compiler independent review, focused build, and standalone runner | **Passed 34/34**, 0 warnings, 0 errors. Review findings for legacy defaults, canonical stability, semantic target ownership, empty menus, and API leakage were fixed and reverified. |
| 2026-08-22 | KitchenSink syntax/compiler/project-assets/localization integration gates | **Passed**: Syntax 94/94, Compiler 34/34, Assets/ProjectModel 44/44, Localization Tooling 6/6. |
| 2026-08-22 | Initial `Velora.Rendering.Sdl` locked build and standalone runner | **Built** with 0 warnings/errors; **18 passed, 1 skipped**. The skipped test requires an unavailable native SDL host library. Independent-review parity findings remain in progress and Phase 10 is not claimed. |

## Known risks and unresolved work

- CQ-002 still needs an explicit migration diagnostic for the input/narration
  grammar ambiguity. CQ-008 is resolved as IH-009 (Runtime owns one pass).
- No authentic historical v1 save has been found; fixture provenance must stay
  explicit.
- Historical v2 saves remain dialogue-oriented. In-memory RuntimeState retains
  control flow, pending input/waits, definitions, screens, emitter intents and
  tweens. A versioned durable format and orchestration with mixer cursors/fades,
  rendered particle pools, effects/transitions, rollback and host state remain.
- CI is defined but has not run on GitHub, so desktop cross-platform claims
  remain pending actual runner evidence.
- No visual Player, Editor, PreviewHost, mobile host or exporter exists in C#
  yet. CLI artifacts are generated locally under ignored `artifacts/cli/`.
- The first SDL adapter has no local real-native render evidence yet and is
  awaiting native host integration despite passing managed parity corrections.

## Next recommended work

1. Extend the remaining numerical and presentation compatibility corpus over
   the working headless Runtime; keep the committed oracle and IH-007 explicit.
2. Extend compatibility snapshots with full runtime state while keeping the
   proven v1/v2 readers and transition-era v2 writer intact.
3. Add a main-thread native SDL host smoke and integrate the real Runtime with
   rendering/text/game UI in the desktop Player.
4. Connect AudioMixerState to the tested miniaudio adapter while preserving
   playback identity/cursors during crossfade track renaming. Complete codec
   and real-device evidence required by ADR-0009.
5. Capture focused oracle traces for nested block pauses, mixed coercion,
   startup preamble, replay choice/input defaults, and documented config gaps.
6. Keep the macOS/Windows/Linux CI matrix synchronized as projects enter the
   solution; report actual runner evidence independently from local checks.
