# Velora Engine — Agent Engineering Contract

This file defines mandatory repository-level instructions for AI coding agents working on Velora Engine.

These rules apply to architecture analysis, planning, implementation, migration, refactoring, testing, documentation, review, debugging, and release work.

Read this file before making substantial changes.

---

# 1. Project Mission

Velora Engine is a cross-platform visual-novel engine and authoring platform.

The current public implementation is a C++20 runtime.

The project is being migrated toward a modern C#/.NET architecture.

The goal of the migration is NOT merely to translate C++ syntax into C#.

The goal is to evolve Velora from a compact visual-novel runtime into a modular development platform containing:

* the VelScript language;
* a compiler/tooling pipeline;
* a headless runtime;
* rendering and audio backends;
* persistence;
* a standalone Player;
* a cross-platform Editor;
* live preview;
* visual authoring;
* UI authoring;
* debugging;
* extension/plugin APIs;
* packaging/export tooling.

Velora must remain focused on visual novels and narrative games.

Do not turn Velora into a general-purpose Unity/Godot replacement.

---

# 2. Current Legacy Baseline

The existing C++ implementation is the behavioral reference during the migration.

Important current technologies include:

* C++20
* CMake
* SDL3
* SDL_image
* SDL_ttf
* miniaudio
* nlohmann/json

Important repository locations include:

```text
README.md
BUILDING.md
CMakeLists.txt

docs/
    velscript.md

assets/
    scripts/
        example.vel

src/
    asset/
    audio/
    core/
    i18n/
    render/
    script/
    text/

tests/
    CoreTests.cpp
```

Before porting a subsystem, inspect its current implementation and existing tests.

Never recreate behavior from memory if the source of truth exists in the repository.

---

# 3. Migration Principle

The C++ implementation is a semantic oracle, not a design template.

Preserve externally observable behavior where compatibility is required.

Do not preserve poor coupling merely because the C++ implementation currently has it.

In particular, avoid reproducing:

* a monolithic God Engine;
* ScriptVM depending directly on the concrete Engine;
* parser structures coupled to rendering;
* SDL types leaking into domain objects;
* rich-text parsing mixed with UI rendering;
* hardcoded application UI embedded inside the engine core;
* runtime code depending on editor code.

The C# architecture should improve these boundaries.

---

# 4. Non-Negotiable Architecture Rules

These rules are mandatory.

## 4.1 Language isolation

Velora language projects must not depend on:

* SDL;
* Avalonia;
* platform-specific graphics APIs;
* editor projects.

The language pipeline must remain usable in headless tools and tests.

## 4.2 Headless runtime

Velora.Runtime must be capable of executing a story without:

* a window;
* GPU access;
* audio hardware;
* Avalonia;
* editor services.

Rendering and audio behavior must be expressed through state and/or interfaces/events.

## 4.3 Editor isolation

Velora.Core and Velora.Runtime must never reference Velora.Editor.

The Editor consumes public engine/language APIs.

## 4.4 Rendering isolation

Rendering backends must implement rendering abstractions.

SDL-specific handles and types must not leak into:

* VelScript syntax;
* semantic models;
* IR;
* save models;
* general runtime state.

## 4.5 Single runtime semantics

The following must share the same runtime semantics:

* standalone Player;
* automated tests;
* Editor Preview;
* debugger;
* CLI simulation tools.

Do not implement a separate fake runtime for the Editor.

## 4.6 Compatibility first

Existing `.vel` scripts must continue to work through the compatibility milestone.

Do not redesign existing syntax while basic migration parity is unfinished.

New language features may be designed after compatibility is established.

---

# 5. Target Technology Direction

Unless superseded by an accepted ADR:

* Language: C#
* Runtime: .NET 10 LTS
* Nullable reference types: enabled
* Editor UI: Avalonia 12.x
* Runtime/window/input backend: SDL3
* Graphics direction: SDL3 GPU abstraction
* Math: System.Numerics where appropriate
* JSON/configuration: System.Text.Json where appropriate
* Tests: modern .NET test framework
* Benchmarks: BenchmarkDotNet where useful
* Release Player: self-contained .NET deployment
* NativeAOT: optional future optimization, not an early migration requirement

Do not pin a new external dependency without first verifying:

* current maintenance status;
* supported platforms;
* license;
* NativeAOT implications where relevant;
* compatibility with target .NET;
* whether the dependency is actually necessary.

Record major dependency choices in an ADR.

---

# 6. Intended Solution Architecture

The desired high-level solution shape is approximately:

```text
Velora.sln

src/
    Velora.Core/
    Velora.ProjectModel/

    Velora.Script.Syntax/
    Velora.Script.Semantics/
    Velora.Script.Compiler/

    Velora.Runtime/

    Velora.Assets/
    Velora.Audio/
    Velora.Localization/
    Velora.Persistence/

    Velora.Rendering.Abstractions/
    Velora.Rendering.Sdl/

    Velora.Plugin.Abstractions/

    Velora.Player/

    Velora.Editor/
    Velora.Editor.Protocol/
    Velora.PreviewHost/

    Velora.Cli/

tests/
    Velora.Script.Tests/
    Velora.Runtime.Tests/
    Velora.Persistence.Tests/
    Velora.Rendering.Tests/
    Velora.Compatibility.Tests/
    Velora.Editor.Tests/

benchmarks/
    Velora.Benchmarks/

samples/
    KitchenSink/
```

This is a target direction, not permission to create empty projects prematurely.

Only introduce projects when their responsibility is justified.

---

# 7. Desired Dependency Direction

Prefer dependencies resembling:

```text
Velora.Script.Syntax
        |
        v
Velora.Script.Semantics
        |
        v
Velora.Script.Compiler
        |
        v
Velora.Runtime

Velora.Runtime
        |
        v
Rendering/Audio abstractions

Rendering abstraction
        |
        v
SDL implementation
```

Forbidden dependency examples:

```text
Velora.Script -> SDL

Velora.Script -> Avalonia

Velora.Runtime -> Velora.Editor

Velora.Core -> Velora.Editor

Parser -> concrete Renderer

Renderer -> Parser

Editor Preview -> independent fake execution engine
```

Avoid circular project dependencies.

---

# 8. VelScript Architecture

The target language pipeline is:

```text
SourceText
    |
    v
Lexer
    |
    v
Lossless Syntax Tree
    |
    v
Semantic Model
    |
    v
Compiler
    |
    v
Velora IR
    |
    v
Runtime
```

Do not directly couple runtime execution to parser syntax nodes in the final architecture.

---

# 9. Source Representation

Editor-grade language tooling requires accurate source information.

Syntax infrastructure should eventually support concepts equivalent to:

```text
SourceText
TextSpan
LinePosition
LinePositionSpan
SyntaxToken
SyntaxTrivia
SyntaxNode
Diagnostic
DiagnosticSeverity
SemanticModel
Symbol
```

Tokens and nodes should retain sufficient source positions for:

* diagnostics;
* syntax highlighting;
* navigation;
* preview-at-caret;
* breakpoints;
* debugger execution highlighting;
* rename/refactoring;
* source maps.

---

# 10. Lossless Editing Requirement

The future visual editor and code editor represent the same VelScript program.

Do not use a destructive workflow such as:

```text
source
 -> AST
 -> visual editor
 -> regenerate entire source file
```

because this destroys:

* comments;
* formatting;
* whitespace choices;
* human structure;
* potentially source-control-friendly diffs.

Prefer a lossless syntax representation and minimal text changes.

Visual modifications should mutate the smallest applicable source region whenever practical.

---

# 11. VelScript Compatibility

Current VelScript behavior is documented primarily in:

```text
docs/velscript.md
```

and implemented under:

```text
src/script/
```

Current language concepts include, among others:

* character definitions;
* dialogue;
* narration;
* variables;
* expressions;
* booleans;
* labels;
* jumps;
* conditions;
* menus;
* text input;
* scenes;
* sprites;
* transitions;
* audio;
* tweens;
* emotions;
* particles;
* screens;
* configuration;
* meta commands.

Before changing syntax or semantics:

1. inspect implementation;
2. inspect tests;
3. inspect documentation;
4. document the intended compatibility change;
5. add tests.

Do not silently reinterpret existing syntax.

---

# 12. VelScript Future Direction

After the C# compatibility milestone, potential language evolution may include:

* modules/imports;
* reusable functions/procedures;
* events;
* arrays;
* maps;
* typed metadata;
* reusable animations;
* sequence blocks;
* parallel blocks;
* camera commands;
* layers;
* materials;
* shader parameters;
* video;
* 2.5D constructs;
* optional 3D constructs;
* extensible plugin-defined commands.

These are future capabilities.

Do not implement them opportunistically during unrelated compatibility work.

---

# 13. No Arbitrary C# in VelScript

Do not introduce unrestricted embedded C# execution into VelScript.

For example, avoid designs equivalent to:

```text
cs {
    arbitrary_code();
}
```

This would damage:

* static analysis;
* sandboxing;
* visual editing;
* deterministic preview;
* portability;
* debugger behavior;
* save compatibility.

VelScript should remain a high-level narrative DSL.

Extensions should use controlled command/plugin APIs instead.

---

# 14. Velora Intermediate Representation

The target runtime should execute an intermediate program representation rather than parser syntax directly.

Representative instructions may include:

```text
SayInstruction
ShowSpriteInstruction
HideSpriteInstruction
ChangeSceneInstruction
SetVariableInstruction
JumpInstruction
BranchInstruction
PresentChoiceInstruction
PlayAudioInstruction
StartTweenInstruction
StartParticleInstruction
ShowScreenInstruction
```

Every executable instruction should be traceable to source through a source map or source span.

Stable instruction identity should be considered where useful for:

* debugging;
* preview;
* execution traces;
* save/resume;
* editor synchronization.

---

# 15. Runtime Design

Velora.Runtime should own narrative execution semantics.

It should not own concrete presentation APIs.

Prefer models such as:

```text
RuntimeState
SceneState
DialogueState
VariableStore
ExecutionState
RuntimeSnapshot
```

and events or commands such as:

```text
DialogueStarted
BackgroundChanged
SpriteShown
SpriteHidden
ChoiceRequested
AudioRequested
EffectRequested
VariableChanged
ExecutionPositionChanged
```

Exact names may differ.

The architectural separation is the requirement.

---

# 16. Determinism and Preview

Runtime execution should be deterministic given the same:

* compiled program;
* initial state;
* user choices;
* inputs;
* deterministic random seed where relevant.

This enables reliable:

* tests;
* preview;
* replay;
* debugging;
* rollback.

Editor preview should be able to reconstruct state at a source location.

A likely architecture is:

```text
source position
    |
    v
source map
    |
    v
IR instruction
    |
    v
nearest cached RuntimeSnapshot
    |
    v
replay
    |
    v
target preview state
```

Do not simply execute an isolated selected line if its state depends on previous instructions.

---

# 17. PreviewHost

The target Editor should not duplicate rendering/runtime implementation.

Prefer a dedicated process such as:

```text
Velora.PreviewHost
```

communicating with:

```text
Velora.Editor
```

through a documented protocol.

Possible messages include:

```text
LoadProject
CompileDocument
PreviewAtSource
Run
Pause
Stop
Advance
SelectChoice
SetVariable
ResizeViewport
```

and responses such as:

```text
FrameReady
RuntimeStateChanged
DiagnosticsChanged
ExecutionPositionChanged
RuntimeError
```

A separate preview process provides crash isolation and preserves runtime parity.

---

# 18. Editor Modes

The long-term Editor has three primary authoring modes.

## Mode 1 — Code

VelScript source editing with:

* syntax highlighting;
* diagnostics;
* completion;
* folding;
* symbol navigation;
* go to definition;
* references;
* rename;
* hover documentation;
* formatting;
* quick fixes where appropriate.

## Mode 2 — Code + Live Preview

Code and real runtime preview displayed together.

Selecting or placing the caret on a relevant statement should allow the Editor to reconstruct and preview the corresponding runtime state.

## Mode 3 — Visual Authoring

A developer should be able to create and edit a visual novel without manually writing source code.

Visual tools may include:

* story-flow editor;
* scene composer;
* dialogue cards;
* choice editor;
* variable/condition editors;
* animation/tween timeline;
* screen/UI designer.

All three modes must operate on the same underlying project and VelScript semantics.

---

# 19. Editor Technology

The expected cross-platform Editor UI direction is Avalonia.

Keep editor-framework concerns isolated from reusable engine libraries.

Avoid placing Avalonia types in:

* language models;
* runtime state;
* project model where unnecessary;
* persistence models.

Code-editor implementations should be hidden behind a small abstraction when reasonable so the entire Editor is not architecturally locked to one editor control.

---

# 20. Rendering

2D remains Velora's default and primary rendering mode.

The rendering architecture should nevertheless avoid blocking future:

* 2.5D;
* perspective cameras;
* depth;
* parallax;
* optional 3D scene elements.

Do not implement 3D before 2D compatibility is stable.

The order is:

1. 2D parity;
2. modern GPU pipeline;
3. improved effects and materials;
4. 2.5D;
5. optional focused 3D features.

Do not build a general-purpose 3D engine unless the project direction explicitly changes.

---

# 21. Rendering Abstraction

Domain models should describe rendering intent, not SDL implementation.

Prefer concepts such as:

```text
Sprite
Transform2D
Color
Material
Camera
Scene
RenderLayer
Effect
```

Concrete SDL handles belong only inside SDL implementation projects.

GPU resources require explicit lifecycle management even in C#.

Do not assume GC safely manages native GPU resources.

Use appropriate ownership and disposal patterns.

---

# 22. Shaders and Post Processing

Current post-effects behavior should be preserved where compatibility requires.

The future architecture should support a proper GPU post-processing pipeline.

Potential effects include:

* vignette;
* CRT;
* glitch;
* blur;
* fade;
* color grading.

Shader source/toolchain decisions must be documented.

Do not scatter backend-specific shader logic throughout runtime code.

---

# 23. Audio

Audio behavior should be abstracted from narrative runtime logic.

Preserve current concepts including:

* master volume;
* BGM;
* SFX;
* voice;
* fades;
* crossfades;
* voice ducking where currently supported.

Runtime requests audio behavior.

A backend performs it.

Do not make ScriptVM own native audio resources.

---

# 24. Assets

Assets must be represented through logical project paths or asset identifiers.

Preserve safe path behavior.

Reject unsafe traversal or unrestricted filesystem access from VelScript.

The future Editor may add:

```text
AssetDatabase
AssetMetadata
AssetImporter
AssetReference
```

Renaming assets in the Editor should eventually allow semantic reference updates rather than silently breaking scripts.

---

# 25. Persistence and Save Compatibility

Existing saves are part of migration compatibility.

Current save formats must be studied before reimplementation.

The C# persistence layer must maintain appropriate readers for historical formats.

Prefer version-specific readers such as:

```text
SaveFileReaderV1
SaveFileReaderV2
SaveFileReaderV3
```

rather than scattering version conditions through unrelated code.

Existing VSAV files should become compatibility fixtures.

Do not delete legacy readers merely because the new runtime uses a newer format.

---

# 26. C++ Compatibility Oracle

Before deleting any migrated C++ subsystem, establish automated parity where practical.

Create compatibility fixtures covering important behavior.

A comprehensive sample such as:

```text
samples/KitchenSink/
```

should eventually exercise:

* dialogue;
* narration;
* variables;
* expressions;
* conditions;
* menus;
* input;
* jumps;
* scenes;
* sprites;
* transitions;
* audio;
* tweens;
* emotions;
* particles;
* screens;
* config;
* meta commands;
* saves;
* persistent variables;
* localization;
* rich text;
* inline commands.

Where possible, produce an expected runtime event trace from the legacy implementation and verify the C# implementation against the same expected semantics.

---

# 27. Testing Strategy

Tests are not optional migration cleanup.

They are part of the migration itself.

Use multiple layers.

## Unit tests

Test components such as:

* lexer;
* parser;
* semantic analysis;
* expressions;
* compiler;
* runtime;
* persistence;
* asset resolution.

## Compatibility tests

Validate C# behavior against established C++ behavior.

## Runtime trace tests

Execute stories headlessly and compare expected events/state.

## Save fixtures

Read actual legacy save files.

## Rendering tests

When practical, use deterministic offscreen rendering and golden/snapshot comparisons with defined tolerances.

## Editor round-trip tests

Test flows equivalent to:

```text
source
 -> parse
 -> visual change
 -> source update
 -> parse
```

and verify semantic correctness.

---

# 28. Build and Test Discipline

Never claim success without verification when the environment allows verification.

For the existing legacy project, inspect documented build commands before using them.

For the C# solution, expected commands will generally include:

```bash
dotnet restore
dotnet build
dotnet test
```

Run focused tests during development and the relevant broader suite before declaring a phase complete.

If a command cannot be executed because of environment limitations, state this explicitly.

Never invent test results.

---

# 29. C# Coding Standards

Use modern idiomatic C# appropriate for the target .NET version.

Enable nullable reference types.

Prefer:

* explicit ownership;
* clear APIs;
* immutable data where useful;
* `record` / `record struct` where semantically appropriate;
* `readonly` where useful;
* `Span<T>` and pooling only where justified;
* async APIs only for genuinely asynchronous work.

Avoid unnecessary abstraction layers.

Avoid enterprise ceremony that provides no value to an engine project.

Do not introduce a dependency-injection framework merely because it is common in web applications.

Use interfaces where they represent real replaceable boundaries.

---

# 30. Performance Rules

Do not assume that C# performance is automatically sufficient.

Do not prematurely micro-optimize either.

Measure.

Frame-critical code should avoid unnecessary allocations.

Avoid repeated per-frame use of allocation-heavy patterns such as:

```text
ToList()
Select()
Where()
string creation
temporary dictionaries
temporary lists
boxing
```

when profiling shows the path matters.

Prefer reused collections, pooling, structs and spans when justified.

Performance-sensitive changes require evidence.

---

# 31. Suggested Performance Targets

Treat these as engineering targets rather than contractual guarantees:

* near-zero avoidable allocations during stable runtime frames;
* runtime update comfortably below the frame budget for typical VN workloads;
* responsive parsing and diagnostics for large VelScript projects;
* normal Editor typing/diagnostics latency that feels immediate;
* preview updates fast enough for interactive authoring.

Benchmark before making significant optimization claims.

---

# 32. Security and Sandboxing

VelScript is untrusted narrative content relative to the host machine.

Preserve restrictions on script-driven filesystem behavior.

Do not allow:

* arbitrary absolute file writes;
* parent-directory traversal;
* arbitrary shell execution;
* arbitrary process launching;
* unrestricted embedded C#;
* unrestricted network access.

Preview mode should be stricter than the normal trusted editor process where appropriate.

C# plugins are trusted code and must be treated separately from VelScript sandbox guarantees.

---

# 33. Plugin Direction

A future plugin API may support controlled extension points such as:

* VelScript commands;
* transitions;
* effects;
* validators;
* asset importers;
* editor panels;
* runtime services.

Prefer metadata-driven extensions.

A command extension should ideally expose enough metadata for Editor tooling to understand:

* command name;
* parameters;
* parameter types;
* defaults;
* documentation;
* category;
* preview capability;
* side-effect classification.

Do not create a plugin API before the underlying boundaries are stable.

---

# 34. Project Format

A future Velora project may resemble:

```text
MyNovel/
    game.velproj

    Scripts/
    Assets/
    UI/
    Localization/
    Plugins/
```

The project model should not be inseparably bound to the Editor UI.

Keep the format simple, versionable and human-readable where practical.

---

# 35. Migration Documentation

Maintain:

```text
docs/migration/ARCHITECTURE.md
docs/migration/COMPATIBILITY.md
docs/migration/ROADMAP.md
docs/migration/STATUS.md
docs/adr/
```

If these files do not exist when migration work begins, create them during the planning/bootstrap phase.

## ARCHITECTURE.md

Describe current approved target architecture and dependency boundaries.

## COMPATIBILITY.md

Track old behavior and whether C# parity exists.

## ROADMAP.md

Track migration phases and ordering.

## STATUS.md

Act as persistent cross-session memory.

It should answer:

* current phase;
* completed work;
* in-progress work;
* blocked work;
* important decisions;
* tests currently passing;
* known failures;
* next recommended task.

Update it at the end of meaningful migration tasks.

---

# 36. Architecture Decision Records

Use ADRs for significant decisions, for example:

```text
ADR-0001-dotnet-runtime.md
ADR-0002-language-pipeline.md
ADR-0003-rendering-backend.md
ADR-0004-editor-framework.md
ADR-0005-preview-architecture.md
ADR-0006-save-compatibility.md
```

An ADR should contain:

* context;
* decision;
* alternatives considered;
* consequences;
* status.

Do not create ADRs for trivial implementation details.

---

# 37. Migration Phases

The expected migration sequence is approximately:

## Phase 0 — Baseline and contracts

* audit the repository;
* document existing architecture;
* expand regression coverage;
* freeze compatibility expectations;
* establish migration docs.

## Phase 1 — .NET foundation

* create .NET solution infrastructure;
* configure builds;
* configure tests;
* configure cross-platform CI where appropriate.

## Phase 2 — VelScript lexical layer

* source text;
* source positions;
* tokens;
* lexer;
* golden token tests.

## Phase 3 — Syntax tree and parser

* lossless syntax infrastructure;
* port current grammar;
* parser diagnostics;
* compatibility tests.

## Phase 4 — Semantic analysis

* symbols;
* label resolution;
* character resolution;
* diagnostics;
* type/argument validation where appropriate.

## Phase 5 — Compiler and IR

* semantic model to runtime IR;
* source map;
* instruction representation.

## Phase 6 — Headless runtime

* variables;
* expressions;
* branching;
* labels;
* dialogue;
* menus;
* input;
* waits.

## Phase 7 — Persistence

* snapshots;
* rollback;
* save/load;
* persistent variables;
* VSAV compatibility.

## Phase 8 — Assets and localization

* safe logical asset resolution;
* project resources;
* localization compatibility.

## Phase 9 — Audio abstraction/backend

* BGM;
* SFX;
* voice;
* fades;
* crossfades;
* ducking.

## Phase 10 — Rendering abstraction and SDL backend

* window/input;
* scene representation;
* backgrounds;
* sprites;
* transforms;
* transitions.

## Phase 11 — Remaining presentation parity

* rich text;
* typewriter;
* menus;
* particles;
* tweens;
* post effects;
* screens.

## Phase 12 — C# Player parity milestone

Current example projects and compatibility fixtures run under the C# Player with expected behavior.

## Phase 13 — Editor shell

* Avalonia application;
* docking/layout;
* project explorer;
* diagnostics/output;
* asset browser foundation.

## Phase 14 — Code authoring

* syntax highlighting;
* diagnostics;
* completion;
* language navigation.

## Phase 15 — PreviewHost

* IPC;
* real runtime;
* frame presentation;
* source synchronization.

## Phase 16 — Runtime debugger

* execution position;
* breakpoints;
* variables;
* stepping where meaningful.

## Phase 17 — Visual story editor

* story flow;
* dialogue;
* choices;
* conditions;
* navigation.

## Phase 18 — Scene composer

* visual sprite placement;
* scene state;
* inspector;
* source synchronization.

## Phase 19 — UI designer

* screens;
* panels;
* text;
* images;
* interactive elements;
* layout/anchors.

## Phase 20 — Asset and localization tooling

* rename/reference tracking;
* unused assets;
* localization UI;
* import/export.

## Phase 21 — Plugin foundations

Only after stable APIs exist.

## Phase 22 — Advanced graphics

* GPU effects;
* 2.5D;
* optional focused 3D support.

Do not skip directly to later phases while fundamental compatibility is incomplete unless explicitly instructed.

---

# 38. AI Agent Workflow

For every non-trivial task:

## Step 1 — Read persistent context

Read:

```text
AGENTS.md
docs/migration/STATUS.md
docs/migration/ARCHITECTURE.md
docs/migration/COMPATIBILITY.md
```

when present and relevant.

## Step 2 — Explore

Inspect the actual relevant implementation.

Do not assume.

Search call sites and tests.

## Step 3 — Define scope

State internally or in the plan:

* what changes;
* what does not change;
* source of truth;
* acceptance conditions.

## Step 4 — Implement smallest coherent unit

Avoid massive unrelated edits.

## Step 5 — Verify

Run formatting/build/tests relevant to the change.

## Step 6 — Review your own diff

Look for:

* accidental scope creep;
* broken boundaries;
* duplicated code;
* missing tests;
* temporary hacks;
* unsafe behavior;
* dead code.

## Step 7 — Update persistent migration state

Update STATUS/COMPATIBILITY/ADR documentation where applicable.

## Step 8 — Report

Report:

* what changed;
* architectural decisions;
* files affected;
* tests executed;
* result;
* unresolved risks;
* recommended next phase.

---

# 39. Context Management

Do not load the entire repository into context without reason.

Start from:

* project instructions;
* migration status;
* task-specific source;
* related tests;
* related documentation.

Expand context when dependencies require it.

For a subsystem migration, inspect both:

1. the subsystem;
2. callers/consumers.

Do not change an API without understanding its consumers.

---

# 40. Repository Hygiene

Do not:

* commit build artifacts;
* commit IDE caches;
* commit secrets;
* commit API keys;
* commit generated binaries unless explicitly intended;
* leave temporary migration scripts without documentation;
* disable tests to make CI pass;
* suppress warnings globally as a shortcut.

Respect `.gitignore`.

---

# 41. Git Discipline

Prefer small coherent commits.

Recommended commit style:

```text
docs: define C# migration compatibility contract
build: scaffold .NET solution
script: port VelScript lexer
script: add lossless syntax tree
runtime: implement headless dialogue execution
persistence: add VSAV v2 reader
rendering: add SDL backend bootstrap
editor: scaffold Avalonia workspace
```

Do not automatically rewrite Git history.

Do not force-push.

Do not delete legacy branches/tags without explicit user instruction.

Do not create commits unless the active tool workflow or user explicitly expects it.

---

# 42. Dependency Discipline

Before introducing a library:

1. determine what problem it solves;
2. check whether .NET/BCL already solves it;
3. verify current maintenance;
4. verify license;
5. verify platform support;
6. consider packaging impact;
7. consider editor/player separation;
8. record major choices.

Avoid dependency accumulation.

---

# 43. Error Handling

Do not hide errors behind broad catch blocks.

Errors should carry actionable context.

VelScript diagnostics should include source locations.

Runtime failures should identify:

* relevant source instruction when possible;
* asset/command involved;
* underlying error.

Editor errors should not crash the entire application for normal malformed script input.

Malformed source is an expected Editor condition.

---

# 44. Logging

Keep reusable engine libraries logging-framework-neutral where reasonable.

Do not fill frame-critical paths with verbose logs.

Diagnostics for authors and developer logs are different concerns.

Do not expose sensitive local paths unnecessarily in distributable game diagnostics.

---

# 45. Documentation

Public APIs and important architectural boundaries require useful documentation.

Do not document obvious implementation line-by-line.

Document:

* contracts;
* invariants;
* formats;
* lifecycle expectations;
* extension points;
* non-obvious behavior.

Keep documentation synchronized with code changes.

---

# 46. Definition of Done

A migration task is not complete merely because code was generated.

It is complete when applicable conditions are satisfied:

* implementation matches approved scope;
* architecture rules are respected;
* code compiles;
* relevant tests pass;
* compatibility behavior is verified;
* no unrelated regressions were introduced;
* no temporary debugging code remains;
* migration documentation is updated;
* own diff has been reviewed.

---

# 47. Forbidden Agent Behaviors

Never:

* rewrite the entire repository blindly in one pass;
* delete C++ implementation before parity exists;
* invent test results;
* invent APIs from external libraries;
* silently change VelScript grammar;
* silently change save formats;
* bypass tests instead of fixing failures;
* add arbitrary embedded C# to VelScript;
* couple Runtime to Editor;
* couple Script projects to SDL;
* create a second incompatible preview runtime;
* build 3D before the 2D migration is stable;
* add large unrelated features during compatibility work;
* replace architecture with generated boilerplate merely to increase progress;
* declare migration complete based only on compilation.

---

# 48. User Communication

Communicate with the user in Russian by default unless the user requests another language.

Repository artifacts should normally use professional English:

* source identifiers;
* code comments;
* commit messages;
* ADRs;
* architecture documentation;
* API documentation.

Explain major architecture choices in understandable terms when reporting them.

---

# 49. Priority Order

When priorities conflict, use this order:

1. correctness;
2. compatibility;
3. architecture integrity;
4. testability;
5. maintainability;
6. security;
7. performance;
8. developer experience;
9. additional features.

Do not sacrifice correctness or architecture merely to report faster progress.

---

# 50. Final Principle

The migration succeeds when Velora becomes easier to build upon without losing what makes Velora itself.

Preserve the product.

Replace the implementation.

Improve the architecture.
