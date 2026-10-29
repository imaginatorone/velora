using System.Collections.ObjectModel;

namespace Velora.Script.Syntax;

public sealed class CompilationUnitSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<StatementSyntax> _statements;

    internal CompilationUnitSyntax(
        SyntaxNodeContext context,
        StatementSyntax[] statements,
        SyntaxToken endOfFileToken)
        : base(SyntaxKind.CompilationUnit, context)
    {
        _statements = Array.AsReadOnly(statements);
        EndOfFileToken = endOfFileToken;
    }

    public IReadOnlyList<StatementSyntax> Statements => _statements;

    public SyntaxToken EndOfFileToken { get; }

    public override IEnumerable<SyntaxNode> ChildNodes() => _statements;
}

public sealed class QualifiedNameSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<SyntaxToken> _identifiers;
    private readonly ReadOnlyCollection<SyntaxToken> _dotTokens;

    internal QualifiedNameSyntax(
        SyntaxNodeContext context,
        SyntaxToken[] identifiers,
        SyntaxToken[] dotTokens)
        : base(SyntaxKind.QualifiedName, context)
    {
        _identifiers = Array.AsReadOnly(identifiers);
        _dotTokens = Array.AsReadOnly(dotTokens);
    }

    public IReadOnlyList<SyntaxToken> Identifiers => _identifiers;

    public IReadOnlyList<SyntaxToken> DotTokens => _dotTokens;

    public string ValueText => string.Join('.', _identifiers.Select(static token => token.ValueText));
}

public sealed class ColorClauseSyntax : SyntaxNode
{
    internal ColorClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken colorNameToken,
        SyntaxToken colonToken,
        SyntaxToken colorToken)
        : base(SyntaxKind.ColorClause, context)
    {
        ColorNameToken = colorNameToken;
        ColonToken = colonToken;
        ColorToken = colorToken;
    }

    public SyntaxToken ColorNameToken { get; }

    public SyntaxToken ColonToken { get; }

    public SyntaxToken ColorToken { get; }
}

public sealed class TransitionClauseSyntax : SyntaxNode
{
    internal TransitionClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken withKeyword,
        SyntaxToken nameToken,
        SyntaxToken? openParenthesisToken,
        SyntaxToken? durationToken,
        SyntaxToken? closeParenthesisToken)
        : base(SyntaxKind.TransitionClause, context)
    {
        WithKeyword = withKeyword;
        NameToken = nameToken;
        OpenParenthesisToken = openParenthesisToken;
        DurationToken = durationToken;
        CloseParenthesisToken = closeParenthesisToken;
    }

    public SyntaxToken WithKeyword { get; }

    public SyntaxToken NameToken { get; }

    public SyntaxToken? OpenParenthesisToken { get; }

    public SyntaxToken? DurationToken { get; }

    public SyntaxToken? CloseParenthesisToken { get; }
}

public sealed class PositionClauseSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<SyntaxToken> _valueTokens;

    internal PositionClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken atKeyword,
        SyntaxToken[] valueTokens)
        : base(SyntaxKind.PositionClause, context)
    {
        AtKeyword = atKeyword;
        _valueTokens = Array.AsReadOnly(valueTokens);
    }

    public SyntaxToken AtKeyword { get; }

    /// <summary>
    /// Gets either one preset identifier or the numeric x/comma/y spelling.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ValueTokens => _valueTokens;
}

/// <summary>
/// A legacy command modifier such as <c>zoom(1.2)</c>, <c>flipped</c>, or <c>at(0.5,0.8)</c>.
/// </summary>
public sealed class ModifierSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<SyntaxToken> _argumentTokens;

    internal ModifierSyntax(
        SyntaxNodeContext context,
        SyntaxToken nameToken,
        SyntaxToken? openParenthesisToken,
        SyntaxToken[] argumentTokens,
        SyntaxToken? closeParenthesisToken)
        : base(SyntaxKind.Modifier, context)
    {
        NameToken = nameToken;
        OpenParenthesisToken = openParenthesisToken;
        _argumentTokens = Array.AsReadOnly(argumentTokens);
        CloseParenthesisToken = closeParenthesisToken;
    }

    public SyntaxToken NameToken { get; }

    public SyntaxToken? OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the exact significant tokens between parentheses, including signs and commas.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ArgumentTokens => _argumentTokens;

    public SyntaxToken? CloseParenthesisToken { get; }

    public bool HasArgumentList => OpenParenthesisToken is not null;
}
