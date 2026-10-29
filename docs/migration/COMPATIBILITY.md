# Velora C++ → C# Compatibility Contract

Status: **language/compiler implemented; headless runtime compatibility in progress**
(2026-10-28). This file records the observable behavior of the current C++
implementation and the migration treatment of that behavior. Only rows whose
implementation evidence is stated explicitly are C# parity claims.

Primary evidence:

- `src/script/Lexer.cpp`, `Parser.cpp`, and `ScriptVM.cpp`;
- `src/core/Engine.cpp`, `GameStateManager.cpp`, and `Paths.cpp`;
- rendering, audio, localization, asset, rich-text, and settings sources under
  `src/`;
- `docs/velscript.md`, `assets/scripts/example.vel`, and
  `tests/CoreTests.cpp`;
- production-VM trace generation in `tests/LegacyRuntimeOracle.cpp` and its
  committed `tests/fixtures/runtime/legacy-runtime-contract.json` result.

The C++ implementation is the semantic oracle, not the C# architecture
template. Compatibility applies to scripts, runtime results, save data, and
author-visible presentation. It does not require preserving SDL coupling,
parser-to-runtime coupling, or the monolithic `Engine` design.

## Classification

| Class | Meaning |
| --- | --- |
| Parity | Preserve through the C# Player parity milestone. |
| Intentional hardening | A documented safety, determinism, or editor-correctness improvement. |
| Parity question | Oracle behavior is known, but preserving it may perpetuate a bug. Resolve with a focused fixture and record the decision before changing it. |
| Future | Deliberately outside the compatibility milestone. |

## VelScript lexical and syntax behavior

| Area | C++ oracle | Migration treatment |
| --- | --- | --- |
| Encoding and locations | Files are treated as UTF-8 bytes. Line and column counters are 1-based; columns count bytes. A UTF-8 BOM is not recognized. | Preserve UTF-8 files. C# spans use UTF-16 code units for editor interoperability; line numbers remain comparable and non-ASCII columns are a documented diagnostic-only deviation. Tolerating a BOM on read is an intentional hardening; writers omit it. |
| Whitespace and comments | Spaces, tabs, and `\r` are discarded. Both `--` and undocumented `//` start a comment anywhere outside a string. Comments are discarded. Consecutive newline tokens are collapsed. | Recognize both comment forms for compatibility, but retain all whitespace, comments, and blank lines as trivia in the lossless tree. |
| Strings | Strings may span lines. Recognized escapes are `\n`, `\t`, `\\`, and `\"`; an unknown escape drops the backslash. Unterminated strings throw. | Preserve accepted strings and decoded values. The C# syntax layer retains raw spelling and reports unterminated input through diagnostics and recovery. |
| Numbers | Decimal digits with at most one dot; forms such as `1.`, `1s`, and `1.5s` tokenize as numbers. A leading minus is a separate unary token. | Parity, including the optional `s` suffix where the consuming grammar currently accepts it. |
| Colors | `#RGB`, `#RRGGBB`, and `#RRGGBBAA` are accepted when all digits are hexadecimal. | Parity. Documentation currently mentions only the latter two forms. |
| Identifiers and keywords | Case-sensitive; keywords are reserved. The implementation's byte-oriented character classification is not a reliable Unicode identifier contract. | Preserve the current ASCII-compatible grammar through parity. Do not silently broaden identifier rules before a language ADR and compatibility tests. |
| Errors | Lexer and parser stop at the first error by throwing. No partial tree exists. | Intentional editor hardening: produce diagnostics, missing/error nodes, and a lossless partial tree instead of throwing for normal malformed author input. |
| Labels | Top-level labels populate one global map. Duplicate names overwrite the earlier entry, so the last definition wins. A missing `jump` target logs an error and execution continues after the jump statement. | Preserve last-wins execution and missing-target continuation through parity, while emitting semantic diagnostics. Whether duplicates become blocking later is CQ-003 below. |
| Blocks | Indentation is ignored; `end` controls `if`, `menu`, `screen`, and `panel`. | Parity. |
| Input ambiguity | After `input "Prompt" -> variable`, immediately following string-literal statements are consumed as choice options until `end`. A narration line directly after text input is therefore ambiguous. | CQ-002. Add an oracle fixture and an author diagnostic before changing recognition. |
| Sprite expression shorthand | An unquoted `show` image expression such as `show mira happy` is joined from identifiers and resolved through character/expression sprite conventions before falling back to a base sprite. | Parity. Keep logical asset resolution separate from syntax. |
| Screen actions | `action` is parsed on screen elements, including `action jump label`, but the current overlay renderer does not execute it. | Preserve parsing; interactive behavior is Future until the UI model defines it. Do not claim legacy interactivity. |

The new language pipeline must additionally satisfy
`Parse(source).ToFullString() == source`. Full fidelity changes representation,
not the recognized legacy program.

## Execution semantics and known quirks

| Area | C++ oracle | Migration treatment |
| --- | --- | --- |
| Startup preamble | If `start` exists, only `character`, `set`, `config`, `meta`, and `screen` statements before the last `start` label execute as initialization; execution then begins after that label. Without `start`, execution begins at statement zero. | Parity, including ordering and the restricted preamble set. External `meta` effects will be mediated by events/services in C#. |
| Dialogue | Dialogue/narration first interpolates `[variable]` in `ScriptVM`; an unknown variable survives that pass. The active Engine then passes text through `RichTextParser`, whose second resolver pass replaces an unknown variable with the empty string. Localization occurs between those passes. Each line pauses for input and captures a rollback snapshot. | Preserve defined-variable formatting. The unknown-variable double-pass is CQ-008: diagnose statically and choose one documented display result rather than accidentally retaining two interpolation owners. Runtime emits state/events rather than calling presentation directly. |
| Nested pauses | `execIf` and selected menu bodies execute every nested statement synchronously. They do not stop when dialogue, `wait`, input, or another menu changes VM state. `selectChoice` then unconditionally sets the VM back to running. | The production behavior is locked in the runtime oracle fixture. C# deliberately uses explicit continuations: the first nested pause suspends execution, resumption continues the remainder of that branch, and choice selection never overwrites a pending state. This resolves CQ-001 as IH-007. |
| Choice replay | Save restoration and preview-like replay choose option index `0` whenever a route decision is needed. | Preserve as the default compatibility route, but make route context explicit and overridable. |
| Values | Values are string, double, or bool. Missing variables yield an empty string. | Parity. |
| Truthiness | `false`, numeric zero, the empty string, and the exact string `"false"` are falsey; other values are truthy. | Parity. |
| Equality and coercion | Equality stringifies operands; doubles use `std::to_string` (normally six fractional digits). Ordering and arithmetic coerce strings/bools to numbers; failed conversion becomes zero. `+` is numeric, and division by zero returns `0`. Boolean `and`/`or` short-circuit. | Parity, with focused mixed-type golden tests. |
| Builtins | `os_username`, `os_time` (`HH:MM` local time), and `os_locale` query ambient platform state. `sys.os_username` is also initialized when a program loads. | Preserve returned values, but inject platform/time services so tests, replay, and preview are deterministic. |
| Name heuristic | Text input sets `sys.name_gibberish` using an ASCII/Cyrillic code-point heuristic for vowels, consonant runs, and repeated characters. Malformed UTF-8 is substituted with U+FFFD. | Parity using `System.Text.Rune`; grapheme-cluster behavior is not introduced during parity. |
| Config | Every key is stored as `__config.<key>`. Despite `docs/velscript.md`, only `ui_accent_color` is read by the engine; script `text_speed` and `game_locale` have no direct effect. | CQ-004. Preserve variable storage; decide explicitly whether to match the oracle no-op or correct the documented keys. |
| Unknown runtime targets | Missing jump labels, screens, assets, tween properties, emotions, particle presets, and meta commands generally warn/skip rather than terminate the story. | Preserve lenient runtime continuation while also producing compile-time diagnostics where resolution is possible. |

## Presentation and supporting systems

- Rendering uses a 1280×720 logical letterboxed surface. Missing backgrounds use
  the procedural fallback; missing sprites are reported and skipped. Named scene
  and sprite transitions currently render as fades. Sprites are height-scaled to
  80% of the viewport, bottom-anchored, x-centered, transformed, and sorted by
  z-order. These laws require trace/golden fixtures before backend replacement.
- Rich-text parsing recognizes bold/color plus inline waits/speed, screen shake,
  SFX, and voice actions. The C++ parser builds styled segments but its active
  render path draws `m_plainText` as one fixed color, so bold/color tags are
  stripped without visible styling. The C# text model retains actual style runs
  as an intentional documentation/correctness restoration for the new renderer.
  Character stepping is by decoded Unicode code point, not UTF-16 code unit or
  grapheme cluster.
- Audio has Master/BGM/SFX/Voice levels. BGM loops; voice and SFX do not. Fade,
  crossfade, stop-fade, and BGM ducking while voice plays are observable. Missing
  optional audio warns and does not stop script execution.
- Localization returns original text for the default locale or a missing/empty
  translation. Non-default lookup uses generated context/speaker keys;
  `translateKey` returns the key when absent. Extraction is currently regex-based
  and is not a grammar oracle. Settings sanitization currently forces locale to
  `en`, another reason not to infer intended locale behavior from UI labels.
- Asset paths are relative to the asset root, not the script. Legacy checks use
  lexical normalization and allow benign intra-root normalization, but they do
  not prove canonical containment through symlinks or mounts. This is intentionally
  hardened below.
- The legacy executable defaults to `assets/scripts/example.vel` and accepts
  `--script/-s`, `--debug/-d`, `--fullscreen/-f`, paired `--width`/`--height`
  values from 360 through 7680, `--extract-locales`, `--help/-h`, and
  `--version`. Help/version exit successfully without starting SDL; argument
  errors exit with code 2. Preserve these launch behaviors where the new Player
  and CLI responsibilities overlap, while adding new build/validate/export
  commands without ambiguity.

## Save and persistent-data formats

### VSAV v2

The file is:

```text
"VSAV" | uint16 little-endian version (= 2) | nlohmann/json CBOR object
```

The object contains `id`, `script_line`, `label`, `background`, `speaker`,
`text`, `text_complete`, `timestamp`, `variables`, `sprites`, and `audio`.
Variables are maps with `{ "type": "string|number|boolean", "value": ... }`.
Sprite records contain `id`, `image`, `x`, `y`, `alpha`, `scale_x`, `scale_y`,
`rotation`, `z_order`, and `flipped`. Audio records contain `channel`, `file`,
`position`, `volume`, and `playing`.

### VSAV v1 reader contract

The header is still `VSAV` plus a two-byte little-endian version marker, but
the payload was written in host byte order. It contains native-endian `uint64`
snapshot id and script line, four length-prefixed strings (label, background,
speaker, text), then variables and basic sprite state. Variable tags are
`0 = string`, `1 = double`, and `2 = bool`; strings use a native-endian
`uint32` byte length. Basic sprites contain id/image plus x, y, alpha, scale-x,
and scale-y. No authentic v1 file has yet been located; see CQ-005.

### VPER v2

Persistent data is:

```text
"VPER" | uint16 little-endian version (= 2) | CBOR typed-variable map
```

The current filename is `persistent.vsav` even though its magic is `VPER`.

### Required validation and slot behavior

- reject files larger than 16 MiB;
- allow at most 10,000 variables, 1,000 sprites, and 128 audio records;
- reject v1 strings larger than 4 MiB;
- preserve manual slots `0..20`; quick save/load maps to slot `0`;
- preserve the in-memory rollback capacity of 200 snapshots;
- add `Auto` as a separate rotating history (default depth 3), never by
  consuming a manual slot.

Snapshots are currently captured only at dialogue. Restore reparses the script,
replays to `script_line`, skips waits, chooses choice zero, submits the first or
empty input, then overlays saved variables/background/sprites/dialogue. Normal
snapshots do not capture enough in-flight menu, input, wait, tween, particle,
screen, history, or audio state for exact restoration; audio is stopped and not
restored. Replay suppresses some destructive meta actions but can repeat other
external/UI side effects. These limitations are oracle facts, not the desired
snapshot model.

Per [ADR-0006](../adr/ADR-0006-save-compatibility.md), C# must read real v2
files and retain a version-specific v1 reader. During the migration window C#
also writes interoperable v2 for the C++ reader; semantic interoperability, not
byte-identical CBOR, is the requirement unless fixtures show otherwise.

Implemented evidence: the C# readers accept the generated C++ VSAV v2 and VPER
v2 fixtures plus the explicitly synthetic v1 contract fixture. The C# v2
writers' output is accepted by the production C++ fixture verifier. Both
directions enforce the documented limits; this closes CQ-006 for the current
fixture corpus without claiming byte-identical CBOR.

## Deviations and intentional hardenings

| ID | Change | Compatibility boundary |
| --- | --- | --- |
| IH-001 | Canonical asset/script-data containment, including symlink and mounted-root escape checks. | Benign paths whose final resolved target remains inside an allowed root continue to work. Absolute and final root escapes fail. See [ADR-0010](../adr/ADR-0010-canonical-path-containment.md). |
| IH-002 | Lossless syntax plus diagnostics/recovery instead of first-error exceptions. | Valid programs keep their semantics and decoded values. |
| IH-003 | UTF-16 source spans and deterministic injected platform/time values. | Diagnostic columns and service wiring may differ; author-visible values remain compatible. |
| IH-004 | Full-state deterministic snapshots and replay without repeating external side effects. | Historical v1/v2 readers remain; new runtime state may require a later save version and ADR. |
| IH-005 | Treat save/project/script inputs as untrusted and enforce schema/count/size limits before allocation or activation. | Existing valid files remain readable. |
| IH-006 | Render the bold/color runs that the legacy rich-text parser already recognizes instead of discarding their styling in the final draw call. | Plain text, tag removal, inline command positions, and Rune-based reveal remain compatible; styled scripts finally match the documented appearance. |
| IH-007 | Correct nested-block suspension and choice continuation. | A nested dialogue, wait, input, or menu now suspends immediately and resumes at the next instruction in the same selected branch. The bundled example has no statement after a nested pause in the same branch, so its routes do not depend on the oracle bug. The exact legacy defect remains reproducible from the committed oracle fixture. |
| IH-008 | Reject an empty `menu` as a compiler error (`VEL3005`) and omit it from recoverable IR. | The legacy VM enters an unrecoverable `WAITING_CHOICE` state with zero options. Valid menus are unchanged; malformed/partial editor source receives a diagnostic and its recoverable IR can continue at the following statement. |
| IH-009 | Runtime owns one variable-interpolation pass before dialogue localization; RichTextParser only parses formatting/inline commands. | Unknown variables display empty, matching the final C++ display. Inserted bracket text is not expanded recursively. Builtin expressions such as `os_locale` must first be assigned to a variable to interpolate, matching C++ variable-only resolution. Translated text is not interpolated a second time. This resolves CQ-008 and prevents a second renderer from changing story semantics. |

## Open compatibility questions

| ID | Question and required evidence |
| --- | --- |
| CQ-002 | Remove the text-input/following-narration ambiguity only with parser fixtures and a migration diagnostic. |
| CQ-003 | Keep duplicate-label last-wins execution through parity; determine later whether the semantic diagnostic should become blocking. |
| CQ-004 | Reconcile documented `text_speed`/`game_locale` effects with the oracle no-op behavior in a deliberate compatibility change. |
| CQ-005 | Continue searching releases/user fixtures for an authentic v1 VSAV. The current `synthetic-v1-host-native` fixture is deliberately labelled as contract-generated, not recovered user data. |
| CQ-007 | Define the first post-v2 save evolution for `ProgramFingerprint`, complete runtime state, and source-aware fallback. Until then, v2 `script_line` semantics remain the interoperability contract. |

## Runtime evidence (2026-10-28)

- `LegacyOracleParityTests` compares mixed values and lenient jump behavior
  against variables in `legacy-runtime-contract.json`. Additional tests cover
  decimal numeric prefixes, incomplete exponents and overflow without truncation.
  Full `std::stod` corner-case parity, notably hexadecimal numeric strings,
  remains outside the proven corpus.
- Nested dialogue/wait/choice suspension follows IH-007. Capture/restore
  preserves branch continuation, effective executed definitions after backward
  jumps, tween return legs, active screens and repeated named emitter intents.
- C++ `ParticleSystem::addEmitter` appends even when names repeat; C# preserves
  that order/multiplicity. Named stop removes all matching active intents.
  Already spawned particles and their fade-out remain the rendering host's state.
- `ScriptVM::interpolateVariables` and the resolver in `Engine.cpp` both read
  stored variables, not expression builtins. IH-009 makes the final empty-name
  behavior explicit and changes recursive replacement to one pass. Focused C#
  tests lock this choice; a full C++ rendered-text oracle is still absent.

## Verification baseline

On 2026-08-22 the updated C++ baseline completed **4/4** CTests:
`VeloraCoreTests`, `VeloraLegacyPersistenceFixtures`,
`VeloraLegacyRuntimeOracle`, and `VeloraVersion`.
The integrated C# solution then passed **222/222** focused tests, including 93
lossless lexer/parser tests, authentic C++ v2 + synthetic v1 persistence reads,
C# v2 round trips, corruption limits, and adversarial asset containment. This
does not yet prove runtime/presentation parity or the open oracle quirks above.
