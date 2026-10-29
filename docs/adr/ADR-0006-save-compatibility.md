# ADR-0006: Preserve Versioned Velora Save Compatibility

## Status

Accepted — 2026-08-22

## Context

The C++ runtime reads VSAV v2 (little-endian header plus CBOR), retains a
native-endian binary v1 reader, and stores persistent variables as VPER v2.
Existing saves are user data and must survive the C# migration. At the same
time, the current snapshot captures only dialogue boundaries and restore
replays source by `script_line`, losing or reconstructing several in-flight
states. Copying that limitation into the new runtime would undermine reliable
save/resume, rollback, preview, and mobile suspend.

## Decision drivers

- read historical user saves without scattering version checks;
- prove transition-era interoperability with the current C++ reader/writer;
- treat files as untrusted input and preserve current safety limits;
- support complete deterministic runtime snapshots and mobile autosave;
- avoid a byte-identical-CBOR requirement unless interoperability evidence
  demands it;
- remain reflection-free in mobile-critical serialization paths.

## Considered options

### Support only a new C# format

This is architecturally clean but discards historical saves and violates the
compatibility contract.

### Reproduce nlohmann/json CBOR bytes exactly

This may be unnecessary and couples the new writer to encoder details rather
than the documented data model. Different canonical ordering/number encodings
can still be semantically interoperable.

### Keep one reader with version conditionals throughout the serializer

This reduces initial files but makes historical behavior fragile and causes
new versions to contaminate current runtime models.

### Use version-specific readers plus a transition-era v2 writer

This isolates historical quirks, supports empirical interop, and allows a later
format to represent the complete runtime without deleting legacy readers.

## Decision

- Retain explicit `SaveFileReaderV1` and `SaveFileReaderV2` responsibilities,
  plus the VPER v2 reader. Do not delete historical readers when a newer writer
  becomes authoritative.
- Implement the exact audited containers and fields documented in
  [COMPATIBILITY.md](../migration/COMPATIBILITY.md). Preserve v2 little-endian
  headers and v1 payload host-endian semantics.
- C# reads authentic C++ v2 files. During the migration window, through C#
  Desktop Player parity and explicit C++ legacy retirement, C# also writes v2
  files the current C++ reader can consume.
- Interoperability is semantic: values, state fields, and resume behavior must
  survive. Byte-identical CBOR is not required unless two-way fixture tests show
  that the C++ reader depends on an encoder detail.
- Preserve the 16 MiB file cap, 10,000-variable, 1,000-sprite, 128-audio-entry,
  v1 4 MiB-string, and manual-slot `0..20` bounds.
- Model targets as `Manual(slot)`, `Quick`, and `Auto`. `Quick` maps to legacy
  slot 0. `Auto` is a separate rotating history with default depth 3 and never
  consumes a manual slot. Mobile suspend requests autosave through Player.Core;
  desktop focus loss alone does not.
- New runtime snapshots contain deterministic execution, variables, dialogue,
  choices/input, scene, waits, active tweens, relevant random state, and other
  state required for exact restore. Presentation/native handles are never
  serialized.
- `InstructionId` identifies an instruction only inside one compiled program.
  It is not persisted as a stable cross-edit identity. A future format may add
  `ProgramFingerprint` and source-aware fallback, but v2 continues to use
  `script_line` during interoperability.
- Serialization is reflection-free in neutral/mobile-critical layers. Readers
  validate magic, version, length, schema, counts, and types before activating
  data.
- Replay used for migration fallback, preview, or restore must not repeat
  external side effects. Resulting runtime state and author-visible warnings are
  explicit.

## Fixture policy

- Generate and retain real C++ v2 VSAV/VPER fixtures, including mixed variable
  types, transformed sprites, audio records, and boundary/corruption cases.
- Run both C++ → C# and C# → current C++ transition tests.
- Search releases and user-provided fixtures for an authentic v1 file. If none
  can be found, create a clearly named **synthetic v1 contract fixture** from
  the audited reader; never label it authentic.

## Consequences

### Positive

- Historical files remain protected by dedicated, testable compatibility code.
- New runtime state is not limited by the legacy `script_line` replay model.
- Mobile lifecycle autosave does not steal legacy manual slots.
- Format evolution can be deliberate and additive.

### Negative

- Persistence must maintain multiple readers and transition tests indefinitely.
- Two-way v2 support temporarily constrains the C# writer to the legacy schema.
- Exact restore across edited scripts remains limited until a later format and
  fingerprint/fallback policy are accepted.

### Risks and mitigations

- **Risk:** no authentic v1 fixture exists. **Mitigation:** keep the reader and
  test the audited binary contract with a conspicuously synthetic fixture.
- **Risk:** semantic CBOR differences break one direction. **Mitigation:** use
  empirical two-way fixtures before declaring interoperability.
- **Risk:** untrusted CBOR causes excessive allocation or partial state changes.
  **Mitigation:** enforce limits during decode and construct state off to the
  side before activation.

## Related decisions

- [ADR-0001](ADR-0001-dotnet-runtime-foundation.md) — reflection-free .NET
  foundation and mobile constraints.
- [ADR-0002](ADR-0002-lossless-language-pipeline.md) — source spans and IR
  source mapping.
