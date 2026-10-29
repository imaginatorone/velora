# ADR-0002: Use a Lossless VelScript Language Pipeline

## Status

Accepted — 2026-08-22

## Context

The C++ lexer discards whitespace/comments, collapses blank lines, and the
parser throws on the first malformed construct. That representation is enough
for the current Player but cannot support diagnostics, minimal visual edits,
navigation, debugging, or code/live-preview synchronization. Regenerating a
file from the legacy AST would destroy comments, formatting, and useful source
control structure.

The migration must preserve valid legacy VelScript semantics while making
partially malformed source a normal Editor state.

## Decision drivers

- exact source round trips and minimal text changes;
- accurate spans for diagnostics, navigation, breakpoints, source maps, and
  preview-at-caret;
- headless language projects with no SDL/Avalonia/platform dependencies;
- compatibility with the audited lexical and parser quirks;
- a simple initial implementation that can be optimized only when measured.

## Considered options

### Port the current token stream and AST directly

This is the smallest runtime port, but it permanently loses trivia and makes
visual/code editing destructive.

### Parse to a semantic AST and regenerate whole files after edits

This enables visual tools quickly but rewrites human formatting and comments,
produces noisy diffs, and creates two competing authoring representations.

### Require Roslyn-style green/red incremental trees immediately

This can support excellent incremental performance, but it adds substantial
complexity before corpus size and typing-latency measurements justify it.

### Build a full-fidelity tree first, behind stable source APIs

This satisfies correctness and editor needs while leaving green/red storage or
incremental parsing as compatible internal optimizations later.

## Decision

Use the following one-way pipeline:

```text
UTF-8 source file
    → SourceText (.NET string)
    → compatibility Lexer (tokens + trivia + diagnostics)
    → lossless Syntax Tree
    → Semantic Model
    → Compiler
    → Velora IR + source map
    → headless Runtime
```

- `Parse(source).ToFullString()` must reproduce `source` exactly, including
  comments, whitespace, blank lines, and raw token spelling.
- Files are UTF-8. A BOM is tolerated on read and omitted on write.
- In-memory spans use zero-based UTF-16 code-unit offsets and lengths. Line
  mapping exposes editor-friendly positions. The C++ oracle's byte-counted
  non-ASCII columns are retained only as a documented diagnostic difference.
- Tokens retain leading/trailing trivia or equivalent full-fidelity ownership;
  nodes retain spans and full spans. Diagnostics carry severity, code, message,
  and source location.
- Malformed source returns a tree with error/missing nodes and diagnostics.
  Exceptions are reserved for programmer/infrastructure failures.
- Semantic analysis owns symbols and resolution. Runtime leniency (for example,
  an unknown jump warning followed by continuation) does not prevent an author
  diagnostic.
- The compiler emits IR; Runtime does not execute parser nodes. Every executable
  instruction maps to a source span and containing label.
- Character-level runtime behavior uses `System.Text.Rune` to match the oracle's
  Unicode code-point stepping and U+FFFD substitution. Grapheme clusters are a
  possible later language/runtime change, not parity behavior.
- Full fidelity is mandatory now. Green/red trees and incremental parsing are
  optional internal improvements after Phase 14 performance evidence.
- Existing syntax is not redesigned during compatibility work. Future modules,
  functions, collections, camera constructs, and plugin commands require their
  own post-parity decisions.

## Consequences

### Positive

- Code and visual authoring share one canonical text representation.
- Editor services and debugger/preview source maps can use one location model.
- Broken scripts remain editable and diagnosable.
- Language tests run without a window, runtime, or native dependencies.

### Negative

- Syntax nodes carry more data than the legacy AST.
- UTF-16 editor positions differ from byte columns on non-ASCII source and need
  explicit conversion at UTF-8 file boundaries.
- Error recovery and minimal-edit behavior require substantially more tests than
  a fail-fast parser.

### Risks and mitigations

- **Risk:** preserving raw text accidentally preserves a legacy parser bug as a
  semantic rule. **Mitigation:** separate token/trivia fidelity from semantic
  compatibility and track questionable behavior in
  [COMPATIBILITY.md](../migration/COMPATIBILITY.md).
- **Risk:** a simple tree is too slow for large projects. **Mitigation:** add
  representative parse/typing benchmarks before changing the public model.

## Related decisions

- [ADR-0001](ADR-0001-dotnet-runtime-foundation.md) — .NET and source-string
  foundation.
- [ADR-0006](ADR-0006-save-compatibility.md) — source mapping and persisted
  execution-position constraints.
