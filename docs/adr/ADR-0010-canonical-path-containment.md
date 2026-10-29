# ADR-0010: Enforce Canonical Path Containment

## Status

Accepted — 2026-08-22

## Context

VelScript and project metadata are untrusted relative to the host machine.
The C++ asset and script-data checks reject absolute paths and lexical parent
traversal after normalization, but lexical checks do not prevent a symlink or
mounted-root target from resolving outside its allowed root. The C# migration
also needs safe logical mounts, read-only packaged assets, writable per-user
script data, and consistent behavior across case-sensitive deployment targets.

## Considered options

- Preserve lexical normalization only. This maintains exact implementation
  behavior but leaves known containment escapes.
- Reject every path that contains `..` before normalization. This is simple but
  unnecessarily rejects benign intra-root paths.
- Resolve both root and candidate canonically, then verify final containment.
  This preserves benign normalization and closes final-target escapes.

## Decision

- Asset identifiers are case-sensitive logical relative paths. Absolute paths
  are not accepted from VelScript.
- Normalize the logical candidate, resolve it against an explicitly allowed
  canonical root, resolve symlinks/reparse points as the platform permits, and
  verify that the final target is the root or a descendant using
  component-aware comparison.
- Apply the same rule after selecting a mounted root; raw string-prefix matching
  is not a containment check.
- Content roots are read-only. Script writes use a separate `IScriptDataStore`
  rooted in per-user data. Parent directories are created only after
  containment has been established.
- Benign normalization such as `sprites/../ui/x.png` may succeed when its final
  resolved target remains inside the selected root. Root escape, symlink escape,
  and mount escape fail with an actionable diagnostic.
- Tests cover absolute paths, sibling-prefix confusion, `..`, symlinks, missing
  targets/parents, mounted roots, platform separators, and filename-case
  mismatches.

## Consequences

- This is an intentional security hardening over the C++ oracle, not an
  accidental compatibility regression.
- Resolution may require more filesystem operations and careful treatment of
  not-yet-created write targets.
- Case mismatches that appeared to work on some developer filesystems are
  diagnosed before Linux/Android deployment.

## Related decisions

- [ADR-0001](ADR-0001-dotnet-runtime-foundation.md) — platform services remain
  outside Runtime.
- [Compatibility contract](../migration/COMPATIBILITY.md#deviations-and-intentional-hardenings)
  — IH-001.
