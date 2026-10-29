using Velora.Script.Syntax.Diagnostics;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax;

internal sealed class Parser
{
    private readonly SourceText _source;
    private readonly SyntaxToken[] _tokens;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position;

    private Parser(SourceText source, SyntaxToken[] tokens)
    {
        _source = source;
        _tokens = tokens;
    }

    public static (CompilationUnitSyntax Root, Diagnostic[] Diagnostics) Parse(
        SourceText source,
        SyntaxToken[] tokens)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tokens);

        Parser parser = new(source, tokens);
        CompilationUnitSyntax root = parser.ParseCompilationUnit();
        return (root, [.. parser._diagnostics]);
    }

    private SyntaxToken Current => Peek(0);

    private SyntaxToken Peek(int offset)
    {
        int index = _position + offset;
        if (index < 0)
        {
            return _tokens[0];
        }

        return index < _tokens.Length ? _tokens[index] : _tokens[^1];
    }

    private SyntaxToken Advance()
    {
        SyntaxToken current = Current;
        if (current.Kind != SyntaxKind.EndOfFileToken)
        {
            _position++;
        }

        return current;
    }

    private bool Match(SyntaxKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    private SyntaxToken MatchToken(SyntaxKind kind, string context)
    {
        if (Current.Kind == kind)
        {
            return Advance();
        }

        string expected = SyntaxFacts.GetText(kind) ?? kind.ToString();
        _diagnostics.Add(new Diagnostic(
            ParserDiagnosticIds.ExpectedToken,
            DiagnosticSeverity.Error,
            $"Expected {expected} in {context}, but found {Describe(Current)}.",
            new TextSpan(Current.Span.Start, 0)));
        return SyntaxToken.CreateMissing(kind, Current.Span.Start);
    }

    private SyntaxToken MatchBlockEnd(string context)
    {
        if (Current.Kind == SyntaxKind.EndKeyword)
        {
            return Advance();
        }

        if (Current.Kind == SyntaxKind.EndOfFileToken)
        {
            _diagnostics.Add(new Diagnostic(
                ParserDiagnosticIds.UnterminatedBlock,
                DiagnosticSeverity.Error,
                $"Unterminated {context}; expected 'end'.",
                new TextSpan(Current.Span.Start, 0)));
            return SyntaxToken.CreateMissing(SyntaxKind.EndKeyword, Current.Span.Start);
        }

        return MatchToken(SyntaxKind.EndKeyword, context);
    }

    private CompilationUnitSyntax ParseCompilationUnit()
    {
        List<StatementSyntax> statements = [];
        SkipNewLines();

        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            int before = _position;
            statements.Add(ParseStatement());
            EnsureProgress(before);
            SkipNewLines();
        }

        SyntaxToken endOfFile = Current;
        return new CompilationUnitSyntax(
            SyntaxNodeContext.FromTokenRange(_source, _tokens, 0, _tokens.Length),
            [.. statements],
            endOfFile);
    }

    private StatementSyntax ParseStatement()
    {
        return Current.Kind switch
        {
            SyntaxKind.CharacterKeyword => ParseCharacterDefinition(),
            SyntaxKind.LabelKeyword => ParseLabelStatement(),
            SyntaxKind.SceneKeyword => ParseSceneStatement(),
            SyntaxKind.ShowKeyword => ParseShowStatement(),
            SyntaxKind.HideKeyword => ParseHideStatement(),
            SyntaxKind.PlayKeyword => ParsePlayStatement(),
            SyntaxKind.StopKeyword => ParseStopStatement(),
            SyntaxKind.WaitKeyword => ParseWaitStatement(),
            SyntaxKind.SetKeyword => ParseSetStatement(),
            SyntaxKind.JumpKeyword => ParseJumpStatement(),
            SyntaxKind.MenuKeyword => ParseMenuStatement(),
            SyntaxKind.IfKeyword => ParseIfStatement(),
            SyntaxKind.ConfigKeyword => ParseConfigStatement(),
            SyntaxKind.TweenKeyword => ParseTweenStatement(),
            SyntaxKind.EmotionKeyword => ParseEmotionStatement(),
            SyntaxKind.InputKeyword => ParseInputStatement(),
            SyntaxKind.MetaKeyword => ParseMetaStatement(),
            SyntaxKind.ParticleKeyword => ParseParticleStatement(),
            SyntaxKind.ScreenKeyword => ParseScreenDefinition(),
            SyntaxKind.IdentifierToken when Current.ValueText == "show_screen" => ParseShowScreenStatement(),
            SyntaxKind.IdentifierToken when Current.ValueText == "hide_screen" => ParseHideScreenStatement(),
            SyntaxKind.IdentifierToken => ParseDialogueStatement(),
            SyntaxKind.StringLiteralToken => ParseNarrationStatement(),
            _ => ParseBadStatement(),
        };
    }

    private CharacterDefinitionStatementSyntax ParseCharacterDefinition()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken identifier = MatchToken(SyntaxKind.IdentifierToken, "character definition");
        SyntaxToken equals = MatchToken(SyntaxKind.EqualsToken, "character definition");
        SyntaxToken displayName = MatchToken(SyntaxKind.StringLiteralToken, "character display name");
        ColorClauseSyntax? color = IsIdentifier("color") ? ParseColorClause() : null;
        return new CharacterDefinitionStatementSyntax(Context(start), keyword, identifier, equals, displayName, color);
    }

    private LabelStatementSyntax ParseLabelStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken name = MatchToken(SyntaxKind.IdentifierToken, "label");
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "label");
        return new LabelStatementSyntax(Context(start), keyword, name, colon);
    }

    private SceneStatementSyntax ParseSceneStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken image = MatchToken(SyntaxKind.StringLiteralToken, "scene image");
        TransitionClauseSyntax? transition = Current.Kind == SyntaxKind.WithKeyword ? ParseTransitionClause() : null;
        return new SceneStatementSyntax(Context(start), keyword, image, transition);
    }

    private ShowStatementSyntax ParseShowStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken target = MatchToken(SyntaxKind.IdentifierToken, "show target");
        List<SyntaxToken> imageTokens = [];

        if (Current.Kind == SyntaxKind.StringLiteralToken)
        {
            imageTokens.Add(Advance());
        }
        else
        {
            while (Current.Kind == SyntaxKind.IdentifierToken && !IsShowModifierName(Current.ValueText))
            {
                imageTokens.Add(Advance());
            }
        }

        PositionClauseSyntax? position = Current.Kind == SyntaxKind.AtKeyword ? ParsePositionClause() : null;
        TransitionClauseSyntax? transition = Current.Kind == SyntaxKind.WithKeyword ? ParseTransitionClause() : null;
        List<ModifierSyntax> modifiers = [];

        while (Current.Kind == SyntaxKind.IdentifierToken && IsShowModifierName(Current.ValueText))
        {
            if (Current.ValueText is "zoom" or "zorder" or "rotate")
            {
                modifiers.Add(ParseSingleNumberModifier(Current.ValueText));
            }
            else
            {
                modifiers.Add(ParseFlagModifier());
            }
        }

        return new ShowStatementSyntax(
            Context(start),
            keyword,
            target,
            [.. imageTokens],
            position,
            transition,
            [.. modifiers]);
    }

    private HideStatementSyntax ParseHideStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken target = MatchToken(SyntaxKind.IdentifierToken, "hide target");
        TransitionClauseSyntax? transition = Current.Kind == SyntaxKind.WithKeyword ? ParseTransitionClause() : null;
        return new HideStatementSyntax(Context(start), keyword, target, transition);
    }

    private PlayStatementSyntax ParsePlayStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken? channel = IsAudioChannel(Current.Kind) ? Advance() : null;
        SyntaxToken file = MatchToken(SyntaxKind.StringLiteralToken, "audio file");
        List<ModifierSyntax> modifiers = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        while (Current.Kind == SyntaxKind.IdentifierToken && IsPlayModifierName(Current.ValueText) && seen.Add(Current.ValueText))
        {
            modifiers.Add(ParseSingleNumberModifier(Current.ValueText));
        }

        return new PlayStatementSyntax(Context(start), keyword, channel, file, [.. modifiers]);
    }

    private StopStatementSyntax ParseStopStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken? channel = IsAudioChannel(Current.Kind) ? Advance() : null;
        ModifierSyntax? fadeOut = IsIdentifier("fadeout") ? ParseSingleNumberModifier("fadeout") : null;
        return new StopStatementSyntax(Context(start), keyword, channel, fadeOut);
    }

    private WaitStatementSyntax ParseWaitStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken duration = MatchToken(SyntaxKind.NumberLiteralToken, "wait duration");
        return new WaitStatementSyntax(Context(start), keyword, duration);
    }

    private SetStatementSyntax ParseSetStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        QualifiedNameSyntax name = ParseQualifiedName("variable name");
        SyntaxToken equals = MatchToken(SyntaxKind.EqualsToken, "set assignment");
        ExpressionSyntax expression = ParseExpression();
        return new SetStatementSyntax(Context(start), keyword, name, equals, expression);
    }

    private JumpStatementSyntax ParseJumpStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken target = MatchToken(SyntaxKind.IdentifierToken, "jump target");
        return new JumpStatementSyntax(Context(start), keyword, target);
    }

    private MenuStatementSyntax ParseMenuStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "menu");
        List<ChoiceClauseSyntax> choices = [];
        SkipNewLines();

        while (Current.Kind is not SyntaxKind.EndKeyword and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind != SyntaxKind.StringLiteralToken)
            {
                ParseBadStatement();
                SkipNewLines();
                continue;
            }

            choices.Add(ParseChoiceClause());
            SkipNewLines();
        }

        SyntaxToken end = MatchBlockEnd("menu block");
        return new MenuStatementSyntax(Context(start), keyword, colon, [.. choices], end);
    }

    private ChoiceClauseSyntax ParseChoiceClause()
    {
        int start = Mark();
        SyntaxToken text = Advance();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "choice");
        SkipNewLines();
        StatementSyntax[] statements = ParseStatementBlock();
        return new ChoiceClauseSyntax(Context(start), text, colon, statements);
    }

    private IfStatementSyntax ParseIfStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        ExpressionSyntax condition = ParseExpression();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "if condition");
        SkipNewLines();
        StatementSyntax[] thenStatements = ParseStatementBlock();
        List<ElifClauseSyntax> elifClauses = [];

        while (Current.Kind == SyntaxKind.ElifKeyword)
        {
            elifClauses.Add(ParseElifClause());
        }

        ElseClauseSyntax? elseClause = Current.Kind == SyntaxKind.ElseKeyword ? ParseElseClause() : null;
        SyntaxToken end = MatchBlockEnd("if block");
        return new IfStatementSyntax(
            Context(start),
            keyword,
            condition,
            colon,
            thenStatements,
            [.. elifClauses],
            elseClause,
            end);
    }

    private ElifClauseSyntax ParseElifClause()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        ExpressionSyntax condition = ParseExpression();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "elif condition");
        SkipNewLines();
        StatementSyntax[] statements = ParseStatementBlock();
        return new ElifClauseSyntax(Context(start), keyword, condition, colon, statements);
    }

    private ElseClauseSyntax ParseElseClause()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "else");
        SkipNewLines();
        StatementSyntax[] statements = ParseStatementBlock();
        return new ElseClauseSyntax(Context(start), keyword, colon, statements);
    }

    private ConfigStatementSyntax ParseConfigStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        QualifiedNameSyntax key = ParseQualifiedName("config key");
        SyntaxToken equals = MatchToken(SyntaxKind.EqualsToken, "config");
        ExpressionSyntax value = ParseExpression();
        return new ConfigStatementSyntax(Context(start), keyword, key, equals, value);
    }

    private TweenStatementSyntax ParseTweenStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken target = MatchToken(SyntaxKind.IdentifierToken, "tween target");
        SyntaxToken property = MatchToken(SyntaxKind.IdentifierToken, "tween property");
        ExpressionSyntax value = ParseExpression();
        ModifierSyntax? duration = IsIdentifier("duration") ? ParseSingleNumberModifier("duration") : null;
        SyntaxToken? easing = Current.Kind == SyntaxKind.IdentifierToken && Current.ValueText != "duration"
            ? Advance()
            : null;
        return new TweenStatementSyntax(Context(start), keyword, target, property, value, duration, easing);
    }

    private EmotionStatementSyntax ParseEmotionStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken character = MatchToken(SyntaxKind.IdentifierToken, "emotion character");
        SyntaxToken emotion = MatchToken(SyntaxKind.IdentifierToken, "emotion type");
        return new EmotionStatementSyntax(Context(start), keyword, character, emotion);
    }

    private DialogueStatementSyntax ParseDialogueStatement()
    {
        int start = Mark();
        SyntaxToken speaker = Advance();
        SyntaxToken text = MatchToken(SyntaxKind.StringLiteralToken, "dialogue text");
        return new DialogueStatementSyntax(Context(start), speaker, text);
    }

    private NarrationStatementSyntax ParseNarrationStatement()
    {
        int start = Mark();
        SyntaxToken text = Advance();
        return new NarrationStatementSyntax(Context(start), text);
    }

    private InputStatementSyntax ParseInputStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken prompt = MatchToken(SyntaxKind.StringLiteralToken, "input prompt");
        SyntaxToken arrow = MatchToken(SyntaxKind.ArrowToken, "input arrow");
        QualifiedNameSyntax target = ParseQualifiedName("input variable");
        List<InputOptionSyntax> options = [];
        SyntaxToken? end = null;

        SkipNewLines();
        if (Current.Kind == SyntaxKind.StringLiteralToken)
        {
            while (Current.Kind == SyntaxKind.StringLiteralToken)
            {
                int optionStart = Mark();
                SyntaxToken text = Advance();
                options.Add(new InputOptionSyntax(Context(optionStart), text));
                SkipNewLines();
            }

            end = MatchBlockEnd("input choice block");
        }

        return new InputStatementSyntax(Context(start), keyword, prompt, arrow, target, [.. options], end);
    }

    private MetaStatementSyntax ParseMetaStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken action = MatchToken(SyntaxKind.IdentifierToken, "meta action");
        List<SyntaxToken> arguments = [];

        while (IsMetaArgument(Current.Kind))
        {
            arguments.Add(Advance());
        }

        return new MetaStatementSyntax(Context(start), keyword, action, [.. arguments]);
    }

    private ParticleStatementSyntax ParseParticleStatement()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken preset = Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken
            ? Advance()
            : MatchToken(SyntaxKind.IdentifierToken, "particle preset");
        SyntaxToken? name = null;
        List<ModifierSyntax> parameters = [];

        if (preset.ValueText == "stop")
        {
            if (Current.Kind is SyntaxKind.IdentifierToken or SyntaxKind.StringLiteralToken)
            {
                name = Advance();
            }
        }
        else
        {
            if (preset.ValueText == "custom" && Current.Kind == SyntaxKind.StringLiteralToken)
            {
                name = Advance();
            }

            while (Current.Kind == SyntaxKind.IdentifierToken || Current.Kind == SyntaxKind.AtKeyword)
            {
                parameters.Add(ParseParticleParameter());
            }
        }

        return new ParticleStatementSyntax(Context(start), keyword, preset, name, [.. parameters]);
    }

    private ScreenDefinitionStatementSyntax ParseScreenDefinition()
    {
        int start = Mark();
        SyntaxToken keyword = Advance();
        SyntaxToken name = MatchToken(SyntaxKind.IdentifierToken, "screen name");
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "screen");
        List<ScreenElementSyntax> elements = [];
        SkipNewLines();

        while (Current.Kind is not SyntaxKind.EndKeyword and not SyntaxKind.EndOfFileToken)
        {
            int before = _position;
            elements.Add(ParseScreenElement());
            EnsureProgress(before);
            SkipNewLines();
        }

        SyntaxToken end = MatchBlockEnd("screen block");
        return new ScreenDefinitionStatementSyntax(Context(start), keyword, name, colon, [.. elements], end);
    }

    private ShowScreenStatementSyntax ParseShowScreenStatement()
    {
        int start = Mark();
        SyntaxToken command = Advance();
        SyntaxToken name = MatchToken(SyntaxKind.IdentifierToken, "screen name");
        return new ShowScreenStatementSyntax(Context(start), command, name);
    }

    private HideScreenStatementSyntax ParseHideScreenStatement()
    {
        int start = Mark();
        SyntaxToken command = Advance();
        SyntaxToken name = MatchToken(SyntaxKind.IdentifierToken, "screen name");
        return new HideScreenStatementSyntax(Context(start), command, name);
    }

    private BadStatementSyntax ParseBadStatement()
    {
        int start = Mark();
        SyntaxToken unexpected = Current;
        _diagnostics.Add(new Diagnostic(
            ParserDiagnosticIds.UnexpectedToken,
            DiagnosticSeverity.Error,
            $"Unexpected token {Describe(unexpected)} at the start of a statement.",
            unexpected.Span));

        List<SyntaxToken> tokens = [Advance()];
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            tokens.Add(Advance());
        }

        return new BadStatementSyntax(Context(start), [.. tokens]);
    }

    private ScreenElementSyntax ParseScreenElement()
    {
        int start = Mark();
        if (Current.Kind == SyntaxKind.TextKeyword)
        {
            SyntaxToken keyword = Advance();
            SyntaxToken text = MatchToken(SyntaxKind.StringLiteralToken, "screen text");
            ScreenPropertySyntax[] properties = ParseScreenProperties();
            return new TextScreenElementSyntax(Context(start), keyword, text, properties);
        }

        if (Current.Kind == SyntaxKind.ImageButtonKeyword)
        {
            SyntaxToken keyword = Advance();
            SyntaxToken image = MatchToken(SyntaxKind.StringLiteralToken, "imagebutton image");
            ScreenPropertySyntax[] properties = ParseScreenProperties();
            return new ImageButtonScreenElementSyntax(Context(start), keyword, image, properties);
        }

        if (Current.Kind == SyntaxKind.PanelKeyword)
        {
            SyntaxToken keyword = Advance();
            ScreenPropertySyntax[] properties = ParseScreenProperties();
            SyntaxToken? colon = null;
            SyntaxToken? end = null;
            List<ScreenElementSyntax> children = [];

            if (Match(SyntaxKind.ColonToken))
            {
                colon = _tokens[_position - 1];
                SkipNewLines();
                while (Current.Kind is not SyntaxKind.EndKeyword and not SyntaxKind.EndOfFileToken)
                {
                    int before = _position;
                    children.Add(ParseScreenElement());
                    EnsureProgress(before);
                    SkipNewLines();
                }

                end = MatchBlockEnd("panel block");
            }

            return new PanelScreenElementSyntax(Context(start), keyword, properties, colon, [.. children], end);
        }

        return ParseBadScreenElement();
    }

    private BadScreenElementSyntax ParseBadScreenElement()
    {
        int start = Mark();
        SyntaxToken unexpected = Current;
        _diagnostics.Add(new Diagnostic(
            ParserDiagnosticIds.UnexpectedScreenElement,
            DiagnosticSeverity.Error,
            $"Expected a text, imagebutton, or panel screen element, but found {Describe(unexpected)}.",
            unexpected.Span));

        List<SyntaxToken> tokens = [Advance()];
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            tokens.Add(Advance());
        }

        return new BadScreenElementSyntax(Context(start), [.. tokens]);
    }

    private ScreenPropertySyntax[] ParseScreenProperties()
    {
        List<ScreenPropertySyntax> properties = [];
        while (true)
        {
            if (Current.Kind == SyntaxKind.AtKeyword)
            {
                int start = Mark();
                SyntaxToken name = Advance();
                List<SyntaxToken> values = [MatchToken(SyntaxKind.NumberLiteralToken, "screen element x position")];
                if (Match(SyntaxKind.CommaToken))
                {
                    values.Add(_tokens[_position - 1]);
                    values.Add(MatchToken(SyntaxKind.NumberLiteralToken, "screen element y position"));
                }

                properties.Add(new ScreenPropertySyntax(Context(start), name, null, null, [.. values], null));
                continue;
            }

            if (IsIdentifier("size"))
            {
                properties.Add(ParseScreenFunctionProperty("size", allowSecondArgument: true, allowNegative: false));
                continue;
            }

            if (IsIdentifier("color"))
            {
                int start = Mark();
                SyntaxToken name = Advance();
                SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "screen element color");
                SyntaxToken value = MatchToken(SyntaxKind.ColorLiteralToken, "screen element color");
                properties.Add(new ScreenPropertySyntax(Context(start), name, colon, null, [value], null));
                continue;
            }

            if (IsIdentifier("alpha"))
            {
                properties.Add(ParseScreenFunctionProperty("alpha", allowSecondArgument: false, allowNegative: true));
                continue;
            }

            if (IsIdentifier("action"))
            {
                int start = Mark();
                SyntaxToken name = Advance();
                List<SyntaxToken> values = [];
                if (Current.Kind == SyntaxKind.JumpKeyword)
                {
                    values.Add(Advance());
                    values.Add(MatchToken(SyntaxKind.IdentifierToken, "screen action target"));
                }
                else
                {
                    values.Add(MatchToken(SyntaxKind.IdentifierToken, "screen action"));
                }

                properties.Add(new ScreenPropertySyntax(Context(start), name, null, null, [.. values], null));
                continue;
            }

            return [.. properties];
        }
    }

    private ScreenPropertySyntax ParseScreenFunctionProperty(
        string name,
        bool allowSecondArgument,
        bool allowNegative)
    {
        int start = Mark();
        SyntaxToken nameToken = Advance();
        SyntaxToken open = MatchToken(SyntaxKind.OpenParenthesisToken, $"{name} arguments");
        List<SyntaxToken> values = [];
        if (allowNegative && Match(SyntaxKind.MinusToken))
        {
            values.Add(_tokens[_position - 1]);
        }

        values.Add(MatchToken(SyntaxKind.NumberLiteralToken, $"{name} value"));
        if (allowSecondArgument && Match(SyntaxKind.CommaToken))
        {
            values.Add(_tokens[_position - 1]);
            values.Add(MatchToken(SyntaxKind.NumberLiteralToken, $"{name} second value"));
        }

        SyntaxToken close = MatchToken(SyntaxKind.CloseParenthesisToken, $"{name} arguments");
        return new ScreenPropertySyntax(Context(start), nameToken, null, open, [.. values], close);
    }

    private ColorClauseSyntax ParseColorClause()
    {
        int start = Mark();
        SyntaxToken name = Advance();
        SyntaxToken colon = MatchToken(SyntaxKind.ColonToken, "color clause");
        SyntaxToken color = MatchToken(SyntaxKind.ColorLiteralToken, "color value");
        return new ColorClauseSyntax(Context(start), name, colon, color);
    }

    private TransitionClauseSyntax ParseTransitionClause()
    {
        int start = Mark();
        SyntaxToken with = Advance();
        SyntaxToken name = MatchToken(SyntaxKind.IdentifierToken, "transition name");
        SyntaxToken? open = null;
        SyntaxToken? duration = null;
        SyntaxToken? close = null;

        if (Match(SyntaxKind.OpenParenthesisToken))
        {
            open = _tokens[_position - 1];
            duration = MatchToken(SyntaxKind.NumberLiteralToken, "transition duration");
            close = MatchToken(SyntaxKind.CloseParenthesisToken, "transition arguments");
        }

        return new TransitionClauseSyntax(Context(start), with, name, open, duration, close);
    }

    private PositionClauseSyntax ParsePositionClause()
    {
        int start = Mark();
        SyntaxToken at = Advance();
        List<SyntaxToken> values = [];

        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            values.Add(Advance());
        }
        else if (Current.Kind == SyntaxKind.NumberLiteralToken)
        {
            values.Add(Advance());
            if (Match(SyntaxKind.CommaToken))
            {
                values.Add(_tokens[_position - 1]);
                values.Add(MatchToken(SyntaxKind.NumberLiteralToken, "y position"));
            }
        }
        else
        {
            values.Add(MatchToken(SyntaxKind.IdentifierToken, "position"));
        }

        return new PositionClauseSyntax(Context(start), at, [.. values]);
    }

    private ModifierSyntax ParseFlagModifier()
    {
        int start = Mark();
        SyntaxToken name = Advance();
        return new ModifierSyntax(Context(start), name, null, [], null);
    }

    private ModifierSyntax ParseSingleNumberModifier(string context, bool allowNegative = true)
    {
        int start = Mark();
        SyntaxToken name = Advance();
        SyntaxToken open = MatchToken(SyntaxKind.OpenParenthesisToken, $"{context} arguments");
        List<SyntaxToken> arguments = [];
        if (allowNegative && Match(SyntaxKind.MinusToken))
        {
            arguments.Add(_tokens[_position - 1]);
        }

        arguments.Add(MatchToken(SyntaxKind.NumberLiteralToken, $"{context} value"));
        SyntaxToken close = MatchToken(SyntaxKind.CloseParenthesisToken, $"{context} arguments");
        return new ModifierSyntax(Context(start), name, open, [.. arguments], close);
    }

    private ModifierSyntax ParseParticleParameter()
    {
        int start = Mark();
        SyntaxToken name = Advance();
        if (!Match(SyntaxKind.OpenParenthesisToken))
        {
            return new ModifierSyntax(Context(start), name, null, [], null);
        }

        SyntaxToken open = _tokens[_position - 1];
        List<SyntaxToken> arguments = [];
        if (name.ValueText is "intensity" or "wind" or "radius")
        {
            arguments.Add(MatchToken(SyntaxKind.NumberLiteralToken, $"{name.ValueText} value"));
        }
        else if (name.ValueText == "at")
        {
            arguments.Add(MatchToken(SyntaxKind.NumberLiteralToken, "particle x position"));
            if (Match(SyntaxKind.CommaToken))
            {
                arguments.Add(_tokens[_position - 1]);
            }

            arguments.Add(MatchToken(SyntaxKind.NumberLiteralToken, "particle y position"));
        }

        SyntaxToken close = MatchToken(SyntaxKind.CloseParenthesisToken, $"{name.ValueText} arguments");
        return new ModifierSyntax(Context(start), name, open, [.. arguments], close);
    }

    private QualifiedNameSyntax ParseQualifiedName(string context)
    {
        int start = Mark();
        List<SyntaxToken> identifiers = [MatchToken(SyntaxKind.IdentifierToken, context)];
        List<SyntaxToken> dots = [];

        while (Match(SyntaxKind.DotToken))
        {
            dots.Add(_tokens[_position - 1]);
            identifiers.Add(MatchToken(SyntaxKind.IdentifierToken, context));
        }

        return new QualifiedNameSyntax(Context(start), [.. identifiers], [.. dots]);
    }

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        int start = Mark();
        ExpressionSyntax left;
        int unaryPrecedence = GetUnaryPrecedence(Current.Kind);

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            SyntaxToken operatorToken = Advance();
            ExpressionSyntax operand = ParseExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(Context(start), operatorToken, operand);
        }
        else
        {
            left = ParsePrimaryExpression();
        }

        while (true)
        {
            int precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
            {
                break;
            }

            SyntaxToken operatorToken = Advance();
            ExpressionSyntax right = ParseExpression(precedence);
            left = new BinaryExpressionSyntax(Context(start), left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        int start = Mark();
        if (Current.Kind == SyntaxKind.OpenParenthesisToken)
        {
            SyntaxToken open = Advance();
            ExpressionSyntax expression = ParseExpression();
            SyntaxToken close = MatchToken(SyntaxKind.CloseParenthesisToken, "parenthesized expression");
            return new ParenthesizedExpressionSyntax(Context(start), open, expression, close);
        }

        if (Current.Kind is SyntaxKind.StringLiteralToken or SyntaxKind.NumberLiteralToken or
            SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.ColorLiteralToken)
        {
            SyntaxToken literal = Advance();
            return new LiteralExpressionSyntax(Context(start), literal);
        }

        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            QualifiedNameSyntax name = ParseQualifiedName("name expression");
            return new NameExpressionSyntax(Context(start), name);
        }

        _diagnostics.Add(new Diagnostic(
            ParserDiagnosticIds.ExpectedExpression,
            DiagnosticSeverity.Error,
            $"Expected an expression, but found {Describe(Current)}.",
            new TextSpan(Current.Span.Start, 0)));
        return new MissingExpressionSyntax(Context(start));
    }

    private StatementSyntax[] ParseStatementBlock()
    {
        List<StatementSyntax> statements = [];
        SkipNewLines();

        while (Current.Kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.EndKeyword and
               not SyntaxKind.ElifKeyword and not SyntaxKind.ElseKeyword && !IsChoiceHeader())
        {
            int before = _position;
            statements.Add(ParseStatement());
            EnsureProgress(before);
            SkipNewLines();
        }

        return [.. statements];
    }

    private void SkipNewLines()
    {
        while (Current.Kind == SyntaxKind.NewLineToken)
        {
            Advance();
        }
    }

    private void EnsureProgress(int previousPosition)
    {
        if (_position == previousPosition && Current.Kind != SyntaxKind.EndOfFileToken)
        {
            Advance();
        }
    }

    private bool IsChoiceHeader() =>
        Current.Kind == SyntaxKind.StringLiteralToken && Peek(1).Kind == SyntaxKind.ColonToken;

    private bool IsIdentifier(string value) =>
        Current.Kind == SyntaxKind.IdentifierToken && Current.ValueText == value;

    private static bool IsShowModifierName(string value) =>
        value is "closeup" or "medium" or "far" or "flipped" or "zoom" or "zorder" or "rotate";

    private static bool IsPlayModifierName(string value) =>
        value is "fadein" or "fadeout" or "volume" or "crossfade";

    private static bool IsAudioChannel(SyntaxKind kind) =>
        kind is SyntaxKind.BgmKeyword or SyntaxKind.SfxKeyword or SyntaxKind.VoiceKeyword;

    private static bool IsMetaArgument(SyntaxKind kind) =>
        kind is SyntaxKind.StringLiteralToken or SyntaxKind.NumberLiteralToken or
            SyntaxKind.IdentifierToken or SyntaxKind.ColorLiteralToken;

    private static int GetUnaryPrecedence(SyntaxKind kind) =>
        kind is SyntaxKind.NotKeyword or SyntaxKind.MinusToken ? 6 : 0;

    private static int GetBinaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.OrKeyword => 1,
        SyntaxKind.AndKeyword => 2,
        SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken or SyntaxKind.LessThanToken or
            SyntaxKind.GreaterThanToken or SyntaxKind.LessThanOrEqualsToken or
            SyntaxKind.GreaterThanOrEqualsToken => 3,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 4,
        SyntaxKind.StarToken or SyntaxKind.SlashToken => 5,
        _ => 0,
    };

    private int Mark() => _position;

    private SyntaxNodeContext Context(int start) =>
        SyntaxNodeContext.FromTokenRange(_source, _tokens, start, _position);

    private static string Describe(SyntaxToken token) => token.Kind == SyntaxKind.EndOfFileToken
        ? "end of file"
        : $"'{token.Text}' ({token.Kind})";
}
