using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Semantics;

/// <summary>
/// A source-backed definition or write that contributes to a symbol.
/// </summary>
public sealed record SymbolDefinition(
    SyntaxNode Syntax,
    TextSpan Span,
    SymbolDefinitionKind Kind);
