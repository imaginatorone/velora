using System.Collections.ObjectModel;
using System.Text;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

public sealed class SyntaxToken
{
    private readonly ReadOnlyCollection<SyntaxTrivia> _leadingTrivia;

    internal SyntaxToken(
        SyntaxKind kind,
        string text,
        string valueText,
        TextSpan span,
        SyntaxTrivia[] leadingTrivia,
        bool isMissing = false)
    {
        Kind = kind;
        Text = text;
        ValueText = valueText;
        Span = span;
        IsMissing = isMissing;
        _leadingTrivia = Array.AsReadOnly(leadingTrivia);
        FullSpan = leadingTrivia.Length == 0
            ? span
            : TextSpan.FromBounds(leadingTrivia[0].Span.Start, span.End);
    }

    public SyntaxKind Kind { get; }

    /// <summary>
    /// Gets the exact token text as it appeared in the source.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the cooked string value for string literals, or <see cref="Text"/> for other tokens.
    /// </summary>
    public string ValueText { get; }

    public TextSpan Span { get; }

    public TextSpan FullSpan { get; }

    /// <summary>
    /// Gets whether the parser synthesized this zero-width token during recovery.
    /// Missing tokens never appear in <see cref="SyntaxTree.Tokens"/> and do not alter source fidelity.
    /// </summary>
    public bool IsMissing { get; }

    public IReadOnlyList<SyntaxTrivia> LeadingTrivia => _leadingTrivia;

    public string ToFullString()
    {
        if (_leadingTrivia.Count == 0)
        {
            return Text;
        }

        StringBuilder builder = new(FullSpan.Length);
        AppendFullString(builder);
        return builder.ToString();
    }

    internal void AppendFullString(StringBuilder builder)
    {
        foreach (SyntaxTrivia trivia in _leadingTrivia)
        {
            builder.Append(trivia.Text);
        }

        builder.Append(Text);
    }

    internal static SyntaxToken CreateMissing(SyntaxKind kind, int position) =>
        new(kind, string.Empty, string.Empty, new TextSpan(position, 0), [], isMissing: true);
}
