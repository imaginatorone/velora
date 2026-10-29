using Velora.Script.Syntax;
using Velora.Script.Syntax.Diagnostics;

namespace Velora.Script.Tests.Parsing;

public sealed class ParserRecoveryTests
{
    [Fact]
    public void MalformedStatementsProduceMissingNodesAndContinueAtFollowingLabel()
    {
        const string source = """
            set = (1 +
            label recovered:
            if true:
                "Still parsed."
            """;

        SyntaxTree? tree = null;
        Exception? exception = Record.Exception(() => tree = SyntaxTree.ParseText(source));

        Assert.Null(exception);
        Assert.NotNull(tree);
        Assert.Equal(source, tree.ToFullString());
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.ExpectedToken);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.ExpectedExpression);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.UnterminatedBlock);

        SetStatementSyntax set = Assert.IsType<SetStatementSyntax>(tree.Root.Statements[0]);
        Assert.True(set.Name.Identifiers[0].IsMissing);
        Assert.IsType<ParenthesizedExpressionSyntax>(set.Expression);
        LabelStatementSyntax label = Assert.IsType<LabelStatementSyntax>(tree.Root.Statements[1]);
        Assert.Equal("recovered", label.Identifier.ValueText);
        Assert.IsType<IfStatementSyntax>(tree.Root.Statements[2]);
    }

    [Fact]
    public void UnexpectedStatementBecomesErrorNodeWithoutDroppingLaterSource()
    {
        const string source = "end stray tokens\nlabel recovered:\n";

        SyntaxTree tree = SyntaxTree.ParseText(source);

        BadStatementSyntax bad = Assert.IsType<BadStatementSyntax>(tree.Root.Statements[0]);
        LabelStatementSyntax label = Assert.IsType<LabelStatementSyntax>(tree.Root.Statements[1]);
        Assert.Equal(3, bad.Tokens.Count);
        Assert.Equal("recovered", label.Identifier.ValueText);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.UnexpectedToken);
        Assert.Equal(source, tree.ToFullString());
    }

    [Fact]
    public void InvalidScreenElementRecoversInsideScreen()
    {
        const string source = """
            screen overlay:
                unsupported value
                text "Recovered"
            end
            label start:
            """;

        SyntaxTree tree = SyntaxTree.ParseText(source);

        ScreenDefinitionStatementSyntax screen = Assert.IsType<ScreenDefinitionStatementSyntax>(tree.Root.Statements[0]);
        Assert.IsType<BadScreenElementSyntax>(screen.Elements[0]);
        Assert.IsType<TextScreenElementSyntax>(screen.Elements[1]);
        Assert.IsType<LabelStatementSyntax>(tree.Root.Statements[1]);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.UnexpectedScreenElement);
        Assert.Equal(source, tree.ToFullString());
    }

    [Fact]
    public void MissingMenuEndReportsStableBlockDiagnostic()
    {
        const string source = "menu:\n    \"Continue\":\n        wait\n";

        SyntaxTree tree = SyntaxTree.ParseText(source);

        MenuStatementSyntax menu = Assert.IsType<MenuStatementSyntax>(Assert.Single(tree.Root.Statements));
        WaitStatementSyntax wait = Assert.IsType<WaitStatementSyntax>(Assert.Single(menu.Choices[0].Statements));
        Assert.True(wait.DurationToken.IsMissing);
        Assert.True(menu.EndKeyword.IsMissing);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.UnterminatedBlock);
        Assert.Contains(tree.Diagnostics, static diagnostic => diagnostic.Id == ParserDiagnosticIds.ExpectedToken);
        Assert.Equal(source, tree.ToFullString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("if:\nend")]
    [InlineData("screen:\npanel:\n")]
    [InlineData("particle rain at(,)")]
    [InlineData("input ->\n\"option\"")]
    public void NormalMalformedAuthorInputNeverThrowsAndAlwaysRoundTrips(string source)
    {
        SyntaxTree? tree = null;

        Exception? exception = Record.Exception(() => tree = SyntaxTree.ParseText(source));

        Assert.Null(exception);
        Assert.NotNull(tree);
        Assert.Equal(source, tree.ToFullString());
    }
}
