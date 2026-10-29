using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Tests.Lexing;

public sealed class LosslessLexingTests
{
    [Fact]
    public void PreservesBomWhitespaceCommentsBlankLinesAndLineEndings()
    {
        const string source = "\uFEFF  -- first\r\n\tlabel start: // second\n\n";

        SyntaxTree tree = SyntaxTree.ParseText(source);

        Assert.Equal(source, tree.ToFullString());
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(SyntaxKind.BomTrivia, tree.Tokens[0].LeadingTrivia[0].Kind);
        Assert.Contains(
            tree.Tokens.SelectMany(token => token.LeadingTrivia),
            trivia => trivia.Kind == SyntaxKind.DashDashCommentTrivia && trivia.Text == "-- first");
        Assert.Contains(
            tree.Tokens.SelectMany(token => token.LeadingTrivia),
            trivia => trivia.Kind == SyntaxKind.SlashSlashCommentTrivia && trivia.Text == "// second");
        Assert.Contains(tree.Tokens, token => token.Kind == SyntaxKind.NewLineToken && token.Text == "\r\n");
        Assert.Equal(3, tree.Tokens.Count(token => token.Kind == SyntaxKind.NewLineToken));
    }

    [Fact]
    public void BundledExampleLexesWithoutLossOrDiagnostics()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "assets", "scripts", "example.vel");
        SourceText source = SourceText.ReadFrom(path);

        SyntaxTree tree = SyntaxTree.ParseText(source);

        Assert.Equal(source.ToString(), tree.ToFullString());
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(SyntaxKind.EndOfFileToken, tree.Tokens[^1].Kind);
        Assert.Contains(tree.Tokens, token => token.Kind == SyntaxKind.LabelKeyword && token.Text == "label");
        Assert.Contains(tree.Tokens, token => token.Kind == SyntaxKind.MenuKeyword && token.Text == "menu");
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
