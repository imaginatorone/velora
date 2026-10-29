using Velora.Script.Syntax;
using Velora.Script.Syntax.Diagnostics;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Tests.Lexing;

public sealed class LexerTests
{
    public static TheoryData<string, SyntaxKind> Keywords => new()
    {
        { "label", SyntaxKind.LabelKeyword },
        { "jump", SyntaxKind.JumpKeyword },
        { "end", SyntaxKind.EndKeyword },
        { "scene", SyntaxKind.SceneKeyword },
        { "show", SyntaxKind.ShowKeyword },
        { "hide", SyntaxKind.HideKeyword },
        { "with", SyntaxKind.WithKeyword },
        { "at", SyntaxKind.AtKeyword },
        { "wait", SyntaxKind.WaitKeyword },
        { "play", SyntaxKind.PlayKeyword },
        { "stop", SyntaxKind.StopKeyword },
        { "bgm", SyntaxKind.BgmKeyword },
        { "sfx", SyntaxKind.SfxKeyword },
        { "voice", SyntaxKind.VoiceKeyword },
        { "menu", SyntaxKind.MenuKeyword },
        { "character", SyntaxKind.CharacterKeyword },
        { "set", SyntaxKind.SetKeyword },
        { "if", SyntaxKind.IfKeyword },
        { "elif", SyntaxKind.ElifKeyword },
        { "else", SyntaxKind.ElseKeyword },
        { "and", SyntaxKind.AndKeyword },
        { "or", SyntaxKind.OrKeyword },
        { "not", SyntaxKind.NotKeyword },
        { "true", SyntaxKind.TrueKeyword },
        { "false", SyntaxKind.FalseKeyword },
        { "config", SyntaxKind.ConfigKeyword },
        { "tween", SyntaxKind.TweenKeyword },
        { "emotion", SyntaxKind.EmotionKeyword },
        { "input", SyntaxKind.InputKeyword },
        { "meta", SyntaxKind.MetaKeyword },
        { "particle", SyntaxKind.ParticleKeyword },
        { "screen", SyntaxKind.ScreenKeyword },
        { "panel", SyntaxKind.PanelKeyword },
        { "text", SyntaxKind.TextKeyword },
        { "imagebutton", SyntaxKind.ImageButtonKeyword },
    };

    public static TheoryData<string, SyntaxKind> FixedTokens => new()
    {
        { "=", SyntaxKind.EqualsToken },
        { "==", SyntaxKind.EqualsEqualsToken },
        { "!=", SyntaxKind.BangEqualsToken },
        { "<", SyntaxKind.LessThanToken },
        { ">", SyntaxKind.GreaterThanToken },
        { "<=", SyntaxKind.LessThanOrEqualsToken },
        { ">=", SyntaxKind.GreaterThanOrEqualsToken },
        { "+", SyntaxKind.PlusToken },
        { "-", SyntaxKind.MinusToken },
        { "*", SyntaxKind.StarToken },
        { "/", SyntaxKind.SlashToken },
        { ".", SyntaxKind.DotToken },
        { "->", SyntaxKind.ArrowToken },
        { ":", SyntaxKind.ColonToken },
        { ",", SyntaxKind.CommaToken },
        { "(", SyntaxKind.OpenParenthesisToken },
        { ")", SyntaxKind.CloseParenthesisToken },
    };

    [Theory]
    [MemberData(nameof(Keywords))]
    public void LexesEveryLegacyKeyword(string text, SyntaxKind expectedKind)
    {
        SyntaxToken token = Assert.Single(SyntaxTree.ParseText(text).Tokens, token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(text, SyntaxFacts.GetText(expectedKind));
    }

    [Theory]
    [MemberData(nameof(FixedTokens))]
    public void LexesEveryFixedToken(string text, SyntaxKind expectedKind)
    {
        SyntaxTree tree = SyntaxTree.ParseText(text);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(text, SyntaxFacts.GetText(expectedKind));
        Assert.Empty(tree.LexerDiagnostics);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.")]
    [InlineData("1s")]
    [InlineData("1.s")]
    [InlineData("0.25")]
    public void PreservesLegacyNumberForms(string text)
    {
        SyntaxToken token = Assert.Single(SyntaxTree.ParseText(text).Tokens, token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(SyntaxKind.NumberLiteralToken, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(text, token.ValueText);
    }

    [Fact]
    public void LeadingDotIsNotPartOfLegacyNumber()
    {
        SyntaxToken[] tokens = [.. SyntaxTree.ParseText(".5").Tokens];

        Assert.Equal(SyntaxKind.DotToken, tokens[0].Kind);
        Assert.Equal(".", tokens[0].ValueText);
        Assert.Equal(SyntaxKind.NumberLiteralToken, tokens[1].Kind);
        Assert.Equal("5", tokens[1].ValueText);
        Assert.Equal(SyntaxKind.EndOfFileToken, tokens[2].Kind);
    }

    [Fact]
    public void KeywordsRemainCaseSensitive()
    {
        SyntaxToken token = Assert.Single(
            SyntaxTree.ParseText("Label").Tokens,
            token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
        Assert.Equal("Label", token.ValueText);
    }

    [Fact]
    public void MatchesRepresentativeLegacySignificantTokenStream()
    {
        const string source = "-- leading\r\nset player.score = 1s + .5 // tail\n\"text\\q\" != #Ab3 and true\n";
        SyntaxTree tree = SyntaxTree.ParseText(source);

        (SyntaxKind Kind, string ValueText)[] actual =
        [
            .. tree.Tokens.Select(token => (token.Kind, token.ValueText)),
        ];
        (SyntaxKind Kind, string ValueText)[] expected =
        [
            (SyntaxKind.NewLineToken, "\r\n"),
            (SyntaxKind.SetKeyword, "set"),
            (SyntaxKind.IdentifierToken, "player"),
            (SyntaxKind.DotToken, "."),
            (SyntaxKind.IdentifierToken, "score"),
            (SyntaxKind.EqualsToken, "="),
            (SyntaxKind.NumberLiteralToken, "1s"),
            (SyntaxKind.PlusToken, "+"),
            (SyntaxKind.DotToken, "."),
            (SyntaxKind.NumberLiteralToken, "5"),
            (SyntaxKind.NewLineToken, "\n"),
            (SyntaxKind.StringLiteralToken, "textq"),
            (SyntaxKind.BangEqualsToken, "!="),
            (SyntaxKind.ColorLiteralToken, "#Ab3"),
            (SyntaxKind.AndKeyword, "and"),
            (SyntaxKind.TrueKeyword, "true"),
            (SyntaxKind.NewLineToken, "\n"),
            (SyntaxKind.EndOfFileToken, string.Empty),
        ];

        Assert.Equal(expected, actual);
        Assert.Empty(tree.LexerDiagnostics);
        Assert.Equal(source, tree.ToFullString());
    }

    [Theory]
    [InlineData("#Ab3")]
    [InlineData("#8FB3A2")]
    [InlineData("#8FB3A2ff")]
    public void LexesSupportedColorWidths(string text)
    {
        SyntaxTree tree = SyntaxTree.ParseText(text);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(SyntaxKind.ColorLiteralToken, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Empty(tree.LexerDiagnostics);
    }

    [Fact]
    public void CooksLegacyStringEscapesIncludingUnknownEscape()
    {
        const string source = "\"line\\n\\t\\\\\\\"\\q\"";

        SyntaxTree tree = SyntaxTree.ParseText(source);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind == SyntaxKind.StringLiteralToken);

        Assert.Equal(source, token.Text);
        Assert.Equal("line\n\t\\\"q", token.ValueText);
        Assert.Empty(tree.LexerDiagnostics);
    }

    [Fact]
    public void AllowsTerminatedMultilineString()
    {
        const string source = "\"first\r\nsecond\"";

        SyntaxTree tree = SyntaxTree.ParseText(source);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind == SyntaxKind.StringLiteralToken);

        Assert.Equal("first\r\nsecond", token.ValueText);
        Assert.Empty(tree.LexerDiagnostics);
        Assert.Equal(source, tree.ToFullString());
    }

    [Fact]
    public void UnterminatedMultilineStringRecoversAtEndOfFile()
    {
        const string source = "  \"first\nsecond";

        SyntaxTree tree = SyntaxTree.ParseText(source);
        Diagnostic diagnostic = Assert.Single(tree.LexerDiagnostics);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind == SyntaxKind.StringLiteralToken);

        Assert.Equal(LexerDiagnosticIds.UnterminatedString, diagnostic.Id);
        Assert.Equal(new TextSpan(2, source.Length - 2), diagnostic.Span);
        Assert.Equal("first\nsecond", token.ValueText);
        Assert.Equal(source, tree.ToFullString());
        Assert.Equal(SyntaxKind.EndOfFileToken, tree.Tokens[^1].Kind);
    }

    [Fact]
    public void UnexpectedCharactersBecomeBadTokensWithStableDiagnostics()
    {
        const string source = "! @ 😀";

        SyntaxTree tree = SyntaxTree.ParseText(source);
        SyntaxToken[] badTokens = [.. tree.Tokens.Where(token => token.Kind == SyntaxKind.BadToken)];

        Assert.Equal(3, badTokens.Length);
        Assert.All(tree.LexerDiagnostics, diagnostic => Assert.Equal(LexerDiagnosticIds.UnexpectedCharacter, diagnostic.Id));
        Assert.Equal(new TextSpan(0, 1), tree.LexerDiagnostics[0].Span);
        Assert.Equal(new TextSpan(2, 1), tree.LexerDiagnostics[1].Span);
        Assert.Equal(new TextSpan(4, 2), tree.LexerDiagnostics[2].Span);
        Assert.Equal(source, tree.ToFullString());
    }

    [Theory]
    [InlineData("#1")]
    [InlineData("#1234")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    public void InvalidColorBecomesBadToken(string source)
    {
        SyntaxTree tree = SyntaxTree.ParseText(source);
        Diagnostic diagnostic = Assert.Single(tree.LexerDiagnostics);
        SyntaxToken token = Assert.Single(tree.Tokens, token => token.Kind != SyntaxKind.EndOfFileToken);

        Assert.Equal(SyntaxKind.BadToken, token.Kind);
        Assert.Equal(LexerDiagnosticIds.InvalidColorLiteral, diagnostic.Id);
        Assert.Equal(new TextSpan(0, source.Length), diagnostic.Span);
        Assert.Equal(source, tree.ToFullString());
    }
}
