using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

/// <summary>
/// Base class for source-backed VelScript syntax. Nodes are immutable views over the original text.
/// </summary>
public abstract class SyntaxNode
{
    private readonly SourceText _source;

    internal SyntaxNode(SyntaxKind kind, SyntaxNodeContext context)
    {
        Kind = kind;
        _source = context.Source;
        Span = context.Span;
        FullSpan = context.FullSpan;
    }

    public SyntaxKind Kind { get; }

    /// <summary>
    /// Gets the token range without trivia preceding the first token.
    /// </summary>
    public TextSpan Span { get; }

    /// <summary>
    /// Gets the exact source range owned by the node, including trivia preceding its first token.
    /// </summary>
    public TextSpan FullSpan { get; }

    public virtual IEnumerable<SyntaxNode> ChildNodes() => [];

    public string ToFullString() => _source.GetText(FullSpan);
}

internal readonly record struct SyntaxNodeContext(SourceText Source, TextSpan Span, TextSpan FullSpan)
{
    public static SyntaxNodeContext FromTokenRange(
        SourceText source,
        IReadOnlyList<SyntaxToken> tokens,
        int startIndex,
        int endIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Count == 0)
        {
            return new SyntaxNodeContext(source, new TextSpan(0, 0), new TextSpan(0, 0));
        }

        int boundedStart = Math.Clamp(startIndex, 0, tokens.Count - 1);
        int boundedEnd = Math.Clamp(endIndex, boundedStart, tokens.Count);
        if (boundedEnd == boundedStart)
        {
            int position = tokens[boundedStart].Span.Start;
            TextSpan empty = new(position, 0);
            return new SyntaxNodeContext(source, empty, empty);
        }

        SyntaxToken first = tokens[boundedStart];
        SyntaxToken last = tokens[boundedEnd - 1];
        TextSpan span = TextSpan.FromBounds(first.Span.Start, last.Span.End);
        TextSpan fullSpan = TextSpan.FromBounds(first.FullSpan.Start, last.Span.End);
        return new SyntaxNodeContext(source, span, fullSpan);
    }
}
