using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Tests.Parsing;

public sealed class ParserTests
{
    [Fact]
    public void BundledExampleParsesLosslesslyWithoutDiagnostics()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "assets", "scripts", "example.vel");
        SourceText source = SourceText.ReadFrom(path);

        SyntaxTree tree = SyntaxTree.ParseText(source);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source.ToString(), tree.ToFullString());
        Assert.Equal(source.ToString(), tree.Root.ToFullString());
        Assert.Equal(SyntaxKind.EndOfFileToken, tree.Root.EndOfFileToken.Kind);

        LabelStatementSyntax[] labels = [.. tree.Root.Statements.OfType<LabelStatementSyntax>()];
        Assert.Equal(
            ["start", "observatory", "operator_call", "summary", "finish"],
            labels.Select(static label => label.Identifier.ValueText));

        Assert.Contains(tree.Root.Statements, static statement => statement is CharacterDefinitionStatementSyntax);
        Assert.Contains(tree.Root.Statements, static statement => statement is MenuStatementSyntax);
        Assert.Contains(tree.Root.Statements, static statement => statement is IfStatementSyntax);
    }

    [Fact]
    public void ParsesEveryLegacyStatementAndCommandShape()
    {
        const string source = """
            character mira = "Mira" color: #ACCC9D
            config ui.theme = #123456
            meta effect vignette 0.6 #ABC
            screen notice:
                panel at 0.1,0.2 size(0.8,0.3) alpha(-0.9):
                    text "Restored" at 0.2,0.3 color: #EDF4E9
                    imagebutton "ui/ok.png" action jump accepted
                end
            end
            label start:
                scene "backgrounds/station.png" with dissolve(0.8s)
                show mira happy smile at left with dissolve(0.4) zoom(1.2) zorder(3) rotate(-5) flipped
                show extra "sprites/extra.png" at 0.25,0.55 far
                hide mira with dissolve(0.3)
                hide all
                play bgm "audio/theme.ogg" fadein(1.0) fadeout(0.2) volume(0.7) crossfade(0.5)
                play "audio/click.wav"
                stop bgm fadeout(1s)
                stop
                wait 0.4s
                set score.total = (1 + 2) * 3
                mira "Dialogue"
                "Narration"
                if score.total >= 3 and not false:
                    emotion mira happy
                elif score.total == 2:
                    tween mira alpha 0.0 duration(0.8) ease_out
                else:
                    jump rejected
                end
                menu:
                    "Accept":
                        jump accepted
                    "Reject":
                        jump rejected
                end
                input "Name" -> player.name
                set input_complete = true
                input "Difficulty" -> difficulty
                    "Story"
                    "Normal"
                end
                particle custom "fog" intensity(0.3) wind(0.1) at(0.5,0.8) radius(0.2)
                particle stop all
                show_screen notice
                hide_screen notice
            label accepted:
                jump finish
            label rejected:
                jump finish
            label finish:
                meta main_menu
            """;

        SyntaxTree tree = SyntaxTree.ParseText(source, "grammar.vel");

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source, tree.ToFullString());

        Assert.IsType<CharacterDefinitionStatementSyntax>(tree.Root.Statements[0]);
        Assert.IsType<ConfigStatementSyntax>(tree.Root.Statements[1]);
        Assert.IsType<MetaStatementSyntax>(tree.Root.Statements[2]);
        ScreenDefinitionStatementSyntax screen = Assert.IsType<ScreenDefinitionStatementSyntax>(tree.Root.Statements[3]);
        PanelScreenElementSyntax panel = Assert.IsType<PanelScreenElementSyntax>(Assert.Single(screen.Elements));
        Assert.Collection(
            panel.Children,
            static child => Assert.IsType<TextScreenElementSyntax>(child),
            static child => Assert.IsType<ImageButtonScreenElementSyntax>(child));

        LabelStatementSyntax startLabel = Assert.IsType<LabelStatementSyntax>(tree.Root.Statements[4]);
        Assert.Equal("start", startLabel.Identifier.ValueText);

        ShowStatementSyntax shorthand = Assert.Single(tree.Root.Statements.OfType<ShowStatementSyntax>(),
            static show => show.TargetToken.ValueText == "mira");
        Assert.Equal("happy_smile", shorthand.ImageValueText);
        Assert.Equal(["zoom", "zorder", "rotate", "flipped"],
            shorthand.Modifiers.Select(static modifier => modifier.NameToken.ValueText));

        IfStatementSyntax conditional = Assert.Single(tree.Root.Statements.OfType<IfStatementSyntax>());
        Assert.Single(conditional.ElifClauses);
        Assert.NotNull(conditional.ElseClause);

        MenuStatementSyntax menu = Assert.Single(tree.Root.Statements.OfType<MenuStatementSyntax>());
        Assert.Equal(["Accept", "Reject"], menu.Choices.Select(static choice => choice.TextToken.ValueText));

        InputStatementSyntax[] inputs = [.. tree.Root.Statements.OfType<InputStatementSyntax>()];
        Assert.False(inputs[0].IsChoice);
        Assert.True(inputs[1].IsChoice);
        Assert.Equal(["Story", "Normal"], inputs[1].Options.Select(static option => option.TextToken.ValueText));

        ParticleStatementSyntax[] particles = [.. tree.Root.Statements.OfType<ParticleStatementSyntax>()];
        Assert.Equal(2, particles.Length);
        Assert.Equal(4, particles[0].Parameters.Count);
        Assert.True(particles[1].StopsAll);
    }

    [Fact]
    public void BuildsLegacyExpressionPrecedenceAndAssociativity()
    {
        SyntaxTree tree = SyntaxTree.ParseText("set value = 1 + 2 * 3 == 7 and not false or true");
        SetStatementSyntax statement = Assert.IsType<SetStatementSyntax>(Assert.Single(tree.Root.Statements));

        BinaryExpressionSyntax logicalOr = Assert.IsType<BinaryExpressionSyntax>(statement.Expression);
        Assert.Equal(SyntaxKind.OrKeyword, logicalOr.OperatorToken.Kind);
        BinaryExpressionSyntax logicalAnd = Assert.IsType<BinaryExpressionSyntax>(logicalOr.Left);
        Assert.Equal(SyntaxKind.AndKeyword, logicalAnd.OperatorToken.Kind);
        BinaryExpressionSyntax comparison = Assert.IsType<BinaryExpressionSyntax>(logicalAnd.Left);
        Assert.Equal(SyntaxKind.EqualsEqualsToken, comparison.OperatorToken.Kind);
        BinaryExpressionSyntax addition = Assert.IsType<BinaryExpressionSyntax>(comparison.Left);
        Assert.Equal(SyntaxKind.PlusToken, addition.OperatorToken.Kind);
        BinaryExpressionSyntax multiplication = Assert.IsType<BinaryExpressionSyntax>(addition.Right);
        Assert.Equal(SyntaxKind.StarToken, multiplication.OperatorToken.Kind);
        UnaryExpressionSyntax negation = Assert.IsType<UnaryExpressionSyntax>(logicalAnd.Right);
        Assert.Equal(SyntaxKind.NotKeyword, negation.OperatorToken.Kind);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void RetainsNodeSpansAndExactLeadingTrivia()
    {
        const string source = "  set player.score = 1 + 2 -- note\r\nlabel next:\r\n";
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SetStatementSyntax statement = Assert.IsType<SetStatementSyntax>(tree.Root.Statements[0]);

        Assert.Equal(new TextSpan(2, 24), statement.Span);
        Assert.Equal(new TextSpan(0, 26), statement.FullSpan);
        Assert.Equal("  set player.score = 1 + 2", statement.ToFullString());
        Assert.Equal(new TextSpan(6, 12), statement.Name.Span);
        Assert.Equal(" player.score", statement.Name.ToFullString());
        Assert.Equal("player.score", tree.Text.GetText(statement.Name.Span));
        Assert.Equal(source, tree.Root.ToFullString());
    }

    [Fact]
    public void PreservesLegacyInputFollowedByNarrationAmbiguity()
    {
        const string source = """
            input "Name" -> player.name
            "This looks like narration."
            end
            "This is narration."
            """;

        SyntaxTree tree = SyntaxTree.ParseText(source);
        InputStatementSyntax input = Assert.IsType<InputStatementSyntax>(tree.Root.Statements[0]);
        NarrationStatementSyntax narration = Assert.IsType<NarrationStatementSyntax>(tree.Root.Statements[1]);

        Assert.True(input.IsChoice);
        Assert.Equal("This looks like narration.", Assert.Single(input.Options).TextToken.ValueText);
        Assert.Equal("This is narration.", narration.TextToken.ValueText);
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source, tree.ToFullString());
    }

    [Fact]
    public void PreservesLegacyParticleParametersWithoutParenthesesAsNoOpFlags()
    {
        const string source = "particle rain intensity wind at radius custom_flag\n";

        SyntaxTree tree = SyntaxTree.ParseText(source);
        ParticleStatementSyntax particle = Assert.IsType<ParticleStatementSyntax>(Assert.Single(tree.Root.Statements));

        Assert.Equal(
            ["intensity", "wind", "at", "radius", "custom_flag"],
            particle.Parameters.Select(static parameter => parameter.NameToken.ValueText));
        Assert.All(particle.Parameters, static parameter => Assert.False(parameter.HasArgumentList));
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source, tree.ToFullString());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string marker = Path.Combine(directory.FullName, "assets", "scripts", "example.vel");
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Velora repository root from the test output directory.");
    }
}
