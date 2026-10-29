using System.Collections.ObjectModel;
using System.Text;
using Velora.Script.Syntax.Diagnostics;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

/// <summary>
/// A full-fidelity, recoverable syntax tree for a VelScript document.
/// </summary>
public sealed class SyntaxTree
{
    private readonly ReadOnlyCollection<SyntaxToken> _tokens;
    private readonly ReadOnlyCollection<Diagnostic> _diagnostics;
    private readonly ReadOnlyCollection<Diagnostic> _lexerDiagnostics;
    private readonly ReadOnlyCollection<Diagnostic> _parserDiagnostics;

    private SyntaxTree(
        SourceText text,
        SyntaxToken[] tokens,
        CompilationUnitSyntax root,
        Diagnostic[] lexerDiagnostics,
        Diagnostic[] parserDiagnostics,
        Diagnostic[] diagnostics)
    {
        Text = text;
        _tokens = Array.AsReadOnly(tokens);
        Root = root;
        _lexerDiagnostics = Array.AsReadOnly(lexerDiagnostics);
        _parserDiagnostics = Array.AsReadOnly(parserDiagnostics);
        _diagnostics = Array.AsReadOnly(diagnostics);
    }

    public SourceText Text { get; }

    public IReadOnlyList<SyntaxToken> Tokens => _tokens;

    public CompilationUnitSyntax Root { get; }

    public IReadOnlyList<Diagnostic> LexerDiagnostics => _lexerDiagnostics;

    public IReadOnlyList<Diagnostic> ParserDiagnostics => _parserDiagnostics;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public static SyntaxTree ParseText(string text, string filePath = "") => ParseText(SourceText.From(text, filePath));

    public static SyntaxTree ParseText(SourceText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        (SyntaxToken[] tokens, Diagnostic[] lexerDiagnostics) = Lexer.Lex(text);
        (CompilationUnitSyntax root, Diagnostic[] parserDiagnostics) = Parser.Parse(text, tokens);
        Diagnostic[] diagnostics =
        [
            .. lexerDiagnostics
                .Concat(parserDiagnostics)
                .OrderBy(static diagnostic => diagnostic.Span.Start)
                .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal),
        ];
        return new SyntaxTree(text, tokens, root, lexerDiagnostics, parserDiagnostics, diagnostics);
    }

    public string ToFullString()
    {
        StringBuilder builder = new(Text.Length);
        foreach (SyntaxToken token in _tokens)
        {
            token.AppendFullString(builder);
        }

        return builder.ToString();
    }
}
