using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Semantics;

/// <summary>
/// A source reference, including unresolved references retained for editor tooling.
/// </summary>
public sealed record SemanticReference(
    string Name,
    SymbolKind TargetKind,
    SemanticReferenceKind Kind,
    SyntaxNode Syntax,
    TextSpan Span,
    Symbol? Symbol)
{
    public bool IsResolved => Symbol is not null;
}
