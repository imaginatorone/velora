# ADR-0011: Shared Execution Identity Boundary

- Status: Accepted
- Date: 2026-08-22

## Context

Compiled Velora IR and public runtime state/events must use the same
`InstructionId` and `ProgramFingerprint` value contracts. The approved
architecture already assigns those contracts to `Velora.Runtime.Contracts`, a
dependency-free package consumed by Runtime, persistence, Player, PreviewHost,
Editor tooling, and debugger consumers.

Phase 5 made the dependency concrete: the compiler creates instruction IDs and
the canonical fingerprint while lowering typed syntax. Its implementation also
reads typed syntax nodes exposed by `SemanticModel`. The original locked graph
listed only `Script.Compiler → Script.Semantics`, which omitted both real
compile-time dependencies and contradicted the ownership described in the
Runtime.Contracts section.

## Decision

1. `Velora.Runtime.Contracts` remains the dependency-free owner of
   `InstructionId` and `ProgramFingerprint`.
2. `Velora.Script.Compiler` may depend directly on
   `Velora.Script.Semantics`, `Velora.Script.Syntax`, and
   `Velora.Runtime.Contracts`.
3. Syntax objects remain compiler implementation inputs. No `SyntaxNode`,
   token, semantic symbol, SDL type, Avalonia type, or platform handle may
   appear in the public IR model.
4. `Velora.Runtime` depends on both the compiler IR and Runtime.Contracts. It
   must not translate between duplicate identity types.
5. This dependency does not authorize Runtime behavior, host services, or
   presentation APIs inside the compiler or Runtime.Contracts.

## Alternatives considered

### Move identity types into Script.Compiler

Rejected. Runtime.Contracts events and snapshots would then either depend on
the compiler or define duplicate identity types. The former reverses the
intended reusable-contract boundary; the latter invites lossy conversions and
inconsistent save/debugger identity.

### Add a dedicated identity project

Rejected for now. A package containing only two tightly related execution
contracts would add project and versioning ceremony without creating a useful
replaceable boundary. If Runtime.Contracts later grows dependencies, a broader
foundation extraction may be reconsidered through another ADR.

### Hide the dependencies through transitive project references

Rejected. The compiler directly names syntax and runtime-contract types. Its
project file should state those dependencies explicitly so architectural
checks and maintainers see the real graph.

## Consequences

- Compiler, Runtime, snapshots, events, persistence, and debugger consumers
  share exact identity values without adapters.
- Runtime.Contracts stays small, immutable, and independent of language or
  presentation implementations.
- The graph correction is additive and acyclic. Language isolation remains
  intact because Runtime.Contracts is a neutral data vocabulary with no SDL,
  Avalonia, filesystem, clock, or host dependencies.
- Architecture tests must continue proving that exported compiler IR does not
  leak syntax types.
