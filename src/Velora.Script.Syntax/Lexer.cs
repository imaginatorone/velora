using System.Text;
using Velora.Script.Syntax.Diagnostics;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

internal sealed class Lexer
{
    private readonly SourceText _source;
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly List<SyntaxToken> _tokens = [];
    private int _position;

    private Lexer(SourceText source)
    {
        _source = source;
    }

    public static (SyntaxToken[] Tokens, Diagnostic[] Diagnostics) Lex(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Lexer lexer = new(source);
        lexer.LexAll();
        return ([.. lexer._tokens], [.. lexer._diagnostics]);
    }

    private bool IsAtEnd => _position >= _source.Length;

    private char Current => Peek(0);

    private char Peek(int offset)
    {
        int position = _position + offset;
        return position >= 0 && position < _source.Length ? _source[position] : '\0';
    }

    private void LexAll()
    {
        while (true)
        {
            SyntaxTrivia[] leadingTrivia = LexLeadingTrivia();
            SyntaxToken token = LexToken(leadingTrivia);
            _tokens.Add(token);

            if (token.Kind == SyntaxKind.EndOfFileToken)
            {
                return;
            }
        }
    }

    private SyntaxTrivia[] LexLeadingTrivia()
    {
        List<SyntaxTrivia> trivia = [];

        while (!IsAtEnd)
        {
            int start = _position;

            if (_position == 0 && Current == '\uFEFF')
            {
                _position++;
                trivia.Add(CreateTrivia(SyntaxKind.BomTrivia, start));
                continue;
            }

            if (Current is ' ' or '\t' || (Current == '\r' && Peek(1) != '\n'))
            {
                do
                {
                    _position++;
                }
                while (!IsAtEnd && (Current is ' ' or '\t' || (Current == '\r' && Peek(1) != '\n')));

                trivia.Add(CreateTrivia(SyntaxKind.WhitespaceTrivia, start));
                continue;
            }

            if (Current == '-' && Peek(1) == '-')
            {
                LexComment();
                trivia.Add(CreateTrivia(SyntaxKind.DashDashCommentTrivia, start));
                continue;
            }

            if (Current == '/' && Peek(1) == '/')
            {
                LexComment();
                trivia.Add(CreateTrivia(SyntaxKind.SlashSlashCommentTrivia, start));
                continue;
            }

            break;
        }

        return [.. trivia];
    }

    private void LexComment()
    {
        _position += 2;
        while (!IsAtEnd && Current is not '\r' and not '\n')
        {
            _position++;
        }
    }

    private SyntaxTrivia CreateTrivia(SyntaxKind kind, int start)
    {
        TextSpan span = TextSpan.FromBounds(start, _position);
        return new SyntaxTrivia(kind, _source.GetText(span), span);
    }

    private SyntaxToken LexToken(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;

        if (IsAtEnd)
        {
            return CreateToken(SyntaxKind.EndOfFileToken, start, string.Empty, leadingTrivia);
        }

        if (Current is '\r' or '\n')
        {
            if (Current == '\r' && Peek(1) == '\n')
            {
                _position += 2;
            }
            else
            {
                _position++;
            }

            return CreateToken(SyntaxKind.NewLineToken, start, valueText: null, leadingTrivia);
        }

        if (Current == '"')
        {
            return LexString(leadingTrivia);
        }

        if (IsAsciiDigit(Current))
        {
            return LexNumber(leadingTrivia);
        }

        if (Current == '#' && IsAsciiHexDigit(Peek(1)))
        {
            return LexColor(leadingTrivia);
        }

        if (IsAsciiLetter(Current) || Current == '_')
        {
            return LexIdentifierOrKeyword(leadingTrivia);
        }

        return LexPunctuationOrBadToken(leadingTrivia);
    }

    private SyntaxToken LexString(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        _position++;
        StringBuilder value = new();
        bool terminated = false;

        while (!IsAtEnd)
        {
            if (Current == '"')
            {
                _position++;
                terminated = true;
                break;
            }

            if (Current != '\\')
            {
                value.Append(Current);
                _position++;
                continue;
            }

            _position++;
            if (IsAtEnd)
            {
                break;
            }

            char escaped = Current;
            value.Append(escaped switch
            {
                'n' => '\n',
                't' => '\t',
                '\\' => '\\',
                '"' => '"',
                _ => escaped,
            });
            _position++;
        }

        TextSpan span = TextSpan.FromBounds(start, _position);
        if (!terminated)
        {
            _diagnostics.Add(new Diagnostic(
                LexerDiagnosticIds.UnterminatedString,
                DiagnosticSeverity.Error,
                "Unterminated string literal.",
                span));
        }

        return CreateToken(SyntaxKind.StringLiteralToken, start, value.ToString(), leadingTrivia);
    }

    private SyntaxToken LexNumber(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        bool hasDecimalPoint = false;

        while (!IsAtEnd && (IsAsciiDigit(Current) || Current == '.'))
        {
            if (Current == '.')
            {
                if (hasDecimalPoint)
                {
                    break;
                }

                hasDecimalPoint = true;
            }

            _position++;
        }

        if (!IsAtEnd && Current == 's')
        {
            _position++;
        }

        return CreateToken(SyntaxKind.NumberLiteralToken, start, valueText: null, leadingTrivia);
    }

    private SyntaxToken LexColor(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        _position++;

        while (!IsAtEnd && IsAsciiHexDigit(Current))
        {
            _position++;
        }

        int digitCount = _position - start - 1;
        if (digitCount is 3 or 6 or 8)
        {
            return CreateToken(SyntaxKind.ColorLiteralToken, start, valueText: null, leadingTrivia);
        }

        TextSpan span = TextSpan.FromBounds(start, _position);
        _diagnostics.Add(new Diagnostic(
            LexerDiagnosticIds.InvalidColorLiteral,
            DiagnosticSeverity.Error,
            "A color literal must contain 3, 6, or 8 hexadecimal digits after '#'.",
            span));
        return CreateToken(SyntaxKind.BadToken, start, valueText: null, leadingTrivia);
    }

    private SyntaxToken LexIdentifierOrKeyword(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        do
        {
            _position++;
        }
        while (!IsAtEnd && (IsAsciiLetterOrDigit(Current) || Current == '_'));

        TextSpan span = TextSpan.FromBounds(start, _position);
        string text = _source.GetText(span);
        SyntaxKind kind = SyntaxFacts.GetKeywordKind(text);
        return new SyntaxToken(kind, text, text, span, leadingTrivia);
    }

    private SyntaxToken LexPunctuationOrBadToken(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        SyntaxKind kind;

        switch (Current)
        {
            case ':' :
                kind = SyntaxKind.ColonToken;
                _position++;
                break;
            case ',' :
                kind = SyntaxKind.CommaToken;
                _position++;
                break;
            case '(' :
                kind = SyntaxKind.OpenParenthesisToken;
                _position++;
                break;
            case ')' :
                kind = SyntaxKind.CloseParenthesisToken;
                _position++;
                break;
            case '+' :
                kind = SyntaxKind.PlusToken;
                _position++;
                break;
            case '-' when Peek(1) == '>':
                kind = SyntaxKind.ArrowToken;
                _position += 2;
                break;
            case '-' :
                kind = SyntaxKind.MinusToken;
                _position++;
                break;
            case '*' :
                kind = SyntaxKind.StarToken;
                _position++;
                break;
            case '/' :
                kind = SyntaxKind.SlashToken;
                _position++;
                break;
            case '.' :
                kind = SyntaxKind.DotToken;
                _position++;
                break;
            case '=' when Peek(1) == '=':
                kind = SyntaxKind.EqualsEqualsToken;
                _position += 2;
                break;
            case '=' :
                kind = SyntaxKind.EqualsToken;
                _position++;
                break;
            case '!' when Peek(1) == '=':
                kind = SyntaxKind.BangEqualsToken;
                _position += 2;
                break;
            case '<' when Peek(1) == '=':
                kind = SyntaxKind.LessThanOrEqualsToken;
                _position += 2;
                break;
            case '<' :
                kind = SyntaxKind.LessThanToken;
                _position++;
                break;
            case '>' when Peek(1) == '=':
                kind = SyntaxKind.GreaterThanOrEqualsToken;
                _position += 2;
                break;
            case '>' :
                kind = SyntaxKind.GreaterThanToken;
                _position++;
                break;
            default:
                return LexBadToken(leadingTrivia);
        }

        return CreateToken(kind, start, valueText: null, leadingTrivia);
    }

    private SyntaxToken LexBadToken(SyntaxTrivia[] leadingTrivia)
    {
        int start = _position;
        int width = char.IsHighSurrogate(Current) && char.IsLowSurrogate(Peek(1)) ? 2 : 1;
        _position += width;
        TextSpan span = new(start, width);
        string text = _source.GetText(span);

        string message = text == "!"
            ? "Unexpected '!'; use '!=' or the 'not' keyword."
            : $"Unexpected character '{text}'.";

        _diagnostics.Add(new Diagnostic(
            LexerDiagnosticIds.UnexpectedCharacter,
            DiagnosticSeverity.Error,
            message,
            span));
        return new SyntaxToken(SyntaxKind.BadToken, text, text, span, leadingTrivia);
    }

    private SyntaxToken CreateToken(
        SyntaxKind kind,
        int start,
        string? valueText,
        SyntaxTrivia[] leadingTrivia)
    {
        TextSpan span = TextSpan.FromBounds(start, _position);
        string text = _source.GetText(span);
        return new SyntaxToken(kind, text, valueText ?? text, span, leadingTrivia);
    }

    private static bool IsAsciiLetter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private static bool IsAsciiLetterOrDigit(char character) => IsAsciiLetter(character) || IsAsciiDigit(character);

    private static bool IsAsciiHexDigit(char character) =>
        IsAsciiDigit(character) || character is >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
