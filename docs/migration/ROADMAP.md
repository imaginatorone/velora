# Velora C# Migration Roadmap

Status: **active execution roadmap** (2026-08-22).

This sequence is a set of engineering and verification gates, not a series of
approval pauses. The requested rewrite proceeds as one continuous run. A later
phase may be prepared in parallel when boundaries are stable, but it is not
reported complete until its exit evidence exists. The C++ oracle remains in
the repository until the user explicitly approves retirement.

## Global gates

- Compatibility work is measured against
  [COMPATIBILITY.md](COMPATIBILITY.md), not memory or documentation alone.
- Every neutral library remains free of SDL, Avalonia, platform-host, ambient
  filesystem, clock, and dynamic-code assumptions as required by its layer.
- No Editor execution path, advanced graphics, or mobile fork may introduce a
  second runtime semantics.
- Each phase exits only after implementation, relevant automated checks,
  self-review, and [STATUS.md](STATUS.md) update. A build alone is insufficient.
- Windows/Linux/mobile status is reported as CI/compile/runtime tested only
  when that evidence actually exists.

## Phases

| Phase | Outcome | Exit evidence |
| --- | --- | --- |
| **0 — Baseline and contracts** | Audit C++ language/runtime/persistence/platform behavior; freeze compatibility classifications; establish architecture, roadmap, status, and initial ADRs; preserve a passing oracle baseline. | Audits and migration docs exist; C++ regression baseline is recorded; unresolved quirks have fixture tasks rather than silent decisions. |
| **1 — .NET foundation** | Pin .NET 10; establish solution/build conventions, justified initial projects, test infrastructure, analyzers, deterministic builds, and desktop CI skeleton. Add compile-smoke hosts only where they prove neutral-layer portability. | Clean restore/build/test on the available SDK; solution graph is acyclic; CI matrix is syntactically valid; platform limitations are recorded. |
| **2 — Lexical layer** | Implement `SourceText`, UTF-16 spans/line mapping, diagnostics, tokens/trivia, and a compatibility lexer that retains all source text. | Golden token/trivia tests cover the bundled script, both comments, escapes, colors, numeric quirks, BOM policy, Unicode locations, malformed input, and exact source preservation. |
| **3 — Lossless syntax and parser** | Build full-fidelity nodes, error recovery, current grammar, and minimal text-change primitives. | `ToFullString()` corpus round trips; statement/block fixtures and malformed-source recovery pass; input ambiguity and duplicate labels are explicitly tested. |
| **4 — Semantic analysis** | Add symbols, scopes, binding, reference indexes, and diagnostics for labels, characters, variables, screens, assets, and command arguments. | Resolution/duplicate/unknown-reference tests pass without coupling syntax to presentation. |
| **5 — Compiler and IR** | Compile the semantic model into source-mapped Velora instructions with within-program `InstructionId` and a program fingerprint contract. | Every executable instruction maps to source; compiler golden tests and IR validation pass; no parser nodes leak into Runtime. |
| **6 — Headless runtime** | Implement deterministic story execution, explicit continuations, variables/expressions, dialogue, choices, input, waits, flow, runtime state, and one-way events. | Headless trace tests match the oracle or a documented hardening; nested pause behavior is resolved; no SDL/Avalonia/filesystem/clock dependency exists. |
| **7 — Persistence and replay** | Implement full snapshots, rollback/rollforward, semantic save targets, rotating autosaves, VPER, VSAV v1/v2 readers, and transition-era v2 writer. | C++↔C# v2 fixtures pass; v1 contract fixture is labelled authentic or synthetic; corruption/limit/slot tests pass; replay does not repeat external side effects. |
| **8 — Assets and localization** | Add case-sensitive logical paths, canonical containment and mounts, catalogs, fallback, extraction, validation, and deterministic asset/localization services. | Traversal/symlink/mount/case tests pass; legacy locale/key behavior is covered; missing references produce diagnostics. |
| **9 — Audio** | Separate runtime audio intent from a replaceable backend; preserve channels, looping, fades, crossfades, volume, ducking, and lifecycle intent. | Headless intent traces and backend lifecycle tests pass; missing optional audio remains non-fatal; dependency choice is verified and recorded. |
| **10 — Rendering abstractions and SDL 2D backend** | Implement explicit resource ownership, window/input surface, logical scaling, backgrounds, sprites, transforms, z-order, and transitions behind abstractions. | Deterministic offscreen tests/goldens cover the 2D laws; no SDL handle crosses the backend boundary. |
| **11 — Presentation parity** | Complete rich text, Rune-based typewriter, inline actions, tweens, particles, post effects, screens, and the platform-neutral game UI model. | KitchenSink traces and rendering/UI goldens cover every supported legacy presentation command. |
| **12 — C# Desktop Player parity gate** | Deliver a standalone desktop Player using the real compiler/runtime/UI/backends, with settings, saves, localization, CLI behavior, and packaged assets. | Bundled and KitchenSink projects run on macOS; desktop build/test/package CI is green on actual macOS/Windows/Linux runners before cross-platform claims. Legacy C++ remains available. |
| **13 — Editor shell** | Create the Avalonia authoring workspace, project model/explorer, diagnostics/output, assets, layout persistence, accessibility baseline, and graceful empty/error states. | Editor launches on the available desktop; workspace and malformed-project tests pass; Editor has no runtime/backend execution dependency. |
| **14 — Code authoring** | Add editor abstraction, highlighting, diagnostics, completion, folding, outline, navigation, references, rename, hover, formatting, and focused fixes. | Editing remains lossless; language-service latency and authoring integration tests pass on representative projects. |
| **15 — PreviewHost** | Run the real Runtime/renderer out of process with versioned control IPC and binary frame transport; support source-position preview via checkpoint and deterministic replay. | Protocol compatibility, crash isolation, route-context replay, resize/locale, and frame transport tests pass; Editor has no in-process preview runtime. |
| **16 — Runtime debugger** | Add execution highlighting, breakpoints, stepping where meaningful, variables/watch, runtime diagnostics, and route context over source maps. | Debug sessions explain current state and remain semantically identical to Player execution. |
| **17 — Visual story editor** | Implement story flow, dialogue, choices, conditions, navigation, and no-code story creation over canonical VelScript/project data. | Visual mutation produces minimal text edits; comments/formatting outside the edit survive; round-trip semantic tests pass. |
| **18 — Scene composer** | Add scene tree, inspector, asset placement, transform/layer editing, background selection, and live runtime preview. | Drag/transform/layer edits round-trip to source and match Player rendering. |
| **19 — Game UI designer** | Edit the shared Velora.UI model for dialogue, menus, save/load, settings, backlog, quickbar, HUD, and custom screens/themes. | Designed UI renders identically in Player and PreviewHost; Avalonia is absent from exported game runtime. |
| **20 — Asset and localization tooling** | Add import, thumbnails, safe rename/move, reference updates, missing/unused/case diagnostics, locale editing, extraction, and validation. | Project-wide reference and localization round-trip tests pass on case-sensitive deployment rules. |
| **21 — Plugin foundations** | Introduce metadata-driven portable/AOT extensions and isolated desktop/editor dynamic extensions only after APIs stabilize. | Capability metadata, validation, trust boundary, mobile compatibility reporting, and lifecycle tests exist; unsupported plugins fail export clearly. |
| **22 — Advanced graphics** | After 2D stability, add SDL3 GPU post-processing, depth-aware layers, parallax/camera/perspective, and an isolated focused 3D scene-node foundation. | 2D fallback remains fully functional; capabilities are explicit; performance/golden evidence supports advanced paths without changing the default product model. |
| **23 — Android and iOS Player/export** | Add real mobile hosts/exporters using the same compiler/runtime/UI/assets/persistence/rendering semantics, with touch, safe areas, orientation, soft keyboard, sandbox storage, lifecycle autosave, audio interruption, and AOT constraints. | Host projects and export validation exist; compile/simulator/device/signing status is reported separately and truthfully. Missing local SDKs do not turn implementation into a TODO. |
| **24 — Export, CI, and release readiness** | Finish CLI/Editor build-export UX, desktop/mobile packaging validation, GitHub Actions matrices, macOS arm64 manual-test artifacts/instructions, and repository hygiene. | `docs/TESTING_MACOS.md` covers the requested manual flow; available local suites pass; actual runner results support platform claims; no generated binaries, commits, or pushes are introduced. |

## Current position

Phases 0 through 5 have exited with local evidence. Phase 6 deterministic
headless runtime now executes both KitchenSink routes with tested capture/restore
and CLI validation/simulation. Remaining compatibility edge cases and complete
save/presentation orchestration are active. Persistence compatibility, semantic save
targets/autosave rotation, safe project/assets, localization/tooling,
audio/rendering/text/game-UI/runtime-contract/platform foundations, the
KitchenSink integration project, and a
production C++ runtime oracle trace are implemented and tested in parallel,
without prematurely claiming their full later-phase parity gates. See
[STATUS.md](STATUS.md) for the exact working-tree and verification state.
