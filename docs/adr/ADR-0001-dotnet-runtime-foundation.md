# ADR-0001: Use .NET 10 for the Migration Foundation

## Status

Accepted — 2026-08-22

## Context

Velora is replacing a C++20/SDL runtime with a modular C# implementation while
retaining the C++ application as a compatibility oracle. The neutral language,
runtime, persistence, asset, localization, and UI-model layers must support
desktop Player/Editor development and later Android/iOS Player hosts. The
foundation must provide nullable analysis, deterministic builds, modern test
tooling, and an AOT-compatible design without forcing NativeAOT during early
parity work.

## Decision drivers

- one supported LTS runtime for the rewrite;
- plain, portable libraries with platform contact isolated in hosts/backends;
- strong compiler/analyzer feedback and reproducible project defaults;
- compatibility with future iOS AOT restrictions;
- no dependency-injection framework or dynamic-code infrastructure without a
  concrete need;
- a test runner compatible with current .NET tooling rather than legacy
  VSTest-only infrastructure.

## Considered options

### Continue the rewrite inside C++

This preserves the current toolchain but does not meet the approved C# product
and tooling direction, and it leaves language/editor integration behind native
presentation coupling.

### Target an older or short-term .NET release

This may have broader preinstalled SDK availability, but it creates an early
upgrade during a long migration and contradicts the approved .NET 10 baseline.

### Target .NET 10 with neutral `net10.0` libraries and host-specific edges

This provides one modern baseline while keeping platform TFMs and workloads out
of pure libraries. Mobile compatibility is established through design rules and
host compile-smoke coverage, not by multi-targeting every library.

### Require NativeAOT immediately

This would find some incompatibilities early but would add packaging/debugging
cost before runtime parity. AOT compatibility can be maintained without making
NativeAOT an initial delivery gate.

## Decision

- Use C# and pin the repository to the .NET SDK `10.0.400` through
  `global.json`, allowing only the latest patch roll-forward.
- Target plain `net10.0` for platform-neutral projects. Use platform-specific
  TFMs only in genuine platform hosts.
- Enable nullable reference types, implicit usings, deterministic builds, .NET
  analyzers, and warnings-as-errors through shared build properties.
- Keep neutral/mobile-critical code reflection-light and free of runtime code
  generation. NativeAOT remains a later optimization; iOS compatibility is a
  design constraint now.
- Use xUnit v3 through `xunit.v3.mtp-v2` 4.0.0 and Microsoft Testing Platform
  for initial tests. Do not add the deprecated xUnit v2/VSTest adapter stack or
  `Microsoft.NET.Test.Sdk` without a demonstrated compatibility need.
- Add projects only when they contain a justified responsibility. An empty
  solution diagram is not an implementation milestone.

## Consequences

### Positive

- Neutral components share one strict build policy and can run headlessly.
- Platform and native dependencies remain visible at host/backend boundaries.
- Analyzer failures are handled as engineering defects instead of accumulated
  migration debt.
- The test foundation aligns with the selected SDK and avoids a legacy adapter
  layer.

### Negative

- Developers need a compatible .NET 10 SDK; it may not be installed system-wide
  yet.
- Android/iOS host builds additionally require workloads and external SDKs not
  implied by the neutral `net10.0` target.
- Warnings-as-errors can expose package/analyzer changes during SDK patch
  updates and requires deliberate dependency pinning.

### Risks and mitigations

- **Risk:** a library later proves incompatible with AOT or a target platform.
  **Mitigation:** apply the dependency checklist before adoption and add mobile
  compile-smoke coverage at the responsible host boundary.
- **Risk:** early scaffolding is mistaken for a working port. **Mitigation:**
  [STATUS.md](../migration/STATUS.md) records projects and executed tests; no
  C# success is inferred from solution files alone.

## Current implementation note

`global.json`, `Directory.Build.props`, `Velora.sln`, and C# repository hygiene
settings have been added. At the time of this ADR, no repository-wide C# build
or test result is claimed. The audited local SDK is an official 10.0.400 SDK in
an isolated `/private/tmp` location; availability on another machine is not
assumed.

## Related decisions

- [ADR-0002](ADR-0002-lossless-language-pipeline.md) — language representation
  and source-coordinate policy.
- [ADR-0006](ADR-0006-save-compatibility.md) — reflection-free historical save
  compatibility.
