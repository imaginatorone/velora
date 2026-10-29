using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

public sealed record SyntaxTrivia
{
    internal SyntaxTrivia(SyntaxKind kind, string text, TextSpan span)
    {
        if (!SyntaxFacts.IsTrivia(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The syntax kind is not trivia.");
        }

        Kind = kind;
        Text = text;
        Span = span;
    }

    public SyntaxKind Kind { get; }

    public string Text { get; }

    public TextSpan Span { get; }

    public string ToFullString() => Text;
}
