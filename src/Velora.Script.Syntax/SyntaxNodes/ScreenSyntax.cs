using System.Collections.ObjectModel;

namespace Velora.Script.Syntax;

public sealed class ScreenPropertySyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<SyntaxToken> _valueTokens;

    internal ScreenPropertySyntax(
        SyntaxNodeContext context,
        SyntaxToken nameToken,
        SyntaxToken? colonToken,
        SyntaxToken? openParenthesisToken,
        SyntaxToken[] valueTokens,
        SyntaxToken? closeParenthesisToken)
        : base(SyntaxKind.ScreenProperty, context)
    {
        NameToken = nameToken;
        ColonToken = colonToken;
        OpenParenthesisToken = openParenthesisToken;
        _valueTokens = Array.AsReadOnly(valueTokens);
        CloseParenthesisToken = closeParenthesisToken;
    }

    public SyntaxToken NameToken { get; }

    public SyntaxToken? ColonToken { get; }

    public SyntaxToken? OpenParenthesisToken { get; }

    public IReadOnlyList<SyntaxToken> ValueTokens => _valueTokens;

    public SyntaxToken? CloseParenthesisToken { get; }
}

public abstract class ScreenElementSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<ScreenPropertySyntax> _properties;

    internal ScreenElementSyntax(
        SyntaxKind kind,
        SyntaxNodeContext context,
        ScreenPropertySyntax[] properties)
        : base(kind, context)
    {
        _properties = Array.AsReadOnly(properties);
    }

    public IReadOnlyList<ScreenPropertySyntax> Properties => _properties;

    protected IEnumerable<SyntaxNode> PropertyNodes() => _properties;
}

public sealed class TextScreenElementSyntax : ScreenElementSyntax
{
    internal TextScreenElementSyntax(
        SyntaxNodeContext context,
        SyntaxToken textKeyword,
        SyntaxToken textToken,
        ScreenPropertySyntax[] properties)
        : base(SyntaxKind.TextScreenElement, context, properties)
    {
        TextKeyword = textKeyword;
        TextToken = textToken;
    }

    public SyntaxToken TextKeyword { get; }

    public SyntaxToken TextToken { get; }

    public override IEnumerable<SyntaxNode> ChildNodes() => PropertyNodes();
}

public sealed class ImageButtonScreenElementSyntax : ScreenElementSyntax
{
    internal ImageButtonScreenElementSyntax(
        SyntaxNodeContext context,
        SyntaxToken imageButtonKeyword,
        SyntaxToken imageToken,
        ScreenPropertySyntax[] properties)
        : base(SyntaxKind.ImageButtonScreenElement, context, properties)
    {
        ImageButtonKeyword = imageButtonKeyword;
        ImageToken = imageToken;
    }

    public SyntaxToken ImageButtonKeyword { get; }

    public SyntaxToken ImageToken { get; }

    public override IEnumerable<SyntaxNode> ChildNodes() => PropertyNodes();
}

public sealed class PanelScreenElementSyntax : ScreenElementSyntax
{
    private readonly ReadOnlyCollection<ScreenElementSyntax> _children;

    internal PanelScreenElementSyntax(
        SyntaxNodeContext context,
        SyntaxToken panelKeyword,
        ScreenPropertySyntax[] properties,
        SyntaxToken? colonToken,
        ScreenElementSyntax[] children,
        SyntaxToken? endKeyword)
        : base(SyntaxKind.PanelScreenElement, context, properties)
    {
        PanelKeyword = panelKeyword;
        ColonToken = colonToken;
        _children = Array.AsReadOnly(children);
        EndKeyword = endKeyword;
    }

    public SyntaxToken PanelKeyword { get; }

    public SyntaxToken? ColonToken { get; }

    public IReadOnlyList<ScreenElementSyntax> Children => _children;

    public SyntaxToken? EndKeyword { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        foreach (SyntaxNode property in PropertyNodes())
        {
            yield return property;
        }

        foreach (ScreenElementSyntax child in _children)
        {
            yield return child;
        }
    }
}

public sealed class BadScreenElementSyntax : ScreenElementSyntax
{
    private readonly ReadOnlyCollection<SyntaxToken> _tokens;

    internal BadScreenElementSyntax(SyntaxNodeContext context, SyntaxToken[] tokens)
        : base(SyntaxKind.BadScreenElement, context, [])
    {
        _tokens = Array.AsReadOnly(tokens);
    }

    public IReadOnlyList<SyntaxToken> Tokens => _tokens;
}

public sealed class ScreenDefinitionStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<ScreenElementSyntax> _elements;

    internal ScreenDefinitionStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken screenKeyword,
        SyntaxToken nameToken,
        SyntaxToken colonToken,
        ScreenElementSyntax[] elements,
        SyntaxToken endKeyword)
        : base(SyntaxKind.ScreenDefinitionStatement, context)
    {
        ScreenKeyword = screenKeyword;
        NameToken = nameToken;
        ColonToken = colonToken;
        _elements = Array.AsReadOnly(elements);
        EndKeyword = endKeyword;
    }

    public SyntaxToken ScreenKeyword { get; }

    public SyntaxToken NameToken { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<ScreenElementSyntax> Elements => _elements;

    public SyntaxToken EndKeyword { get; }

    public override IEnumerable<SyntaxNode> ChildNodes() => _elements;
}
