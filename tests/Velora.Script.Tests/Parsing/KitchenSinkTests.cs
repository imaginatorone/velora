using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Tests.Parsing;

public sealed class KitchenSinkTests
{
    [Fact]
    public void KitchenSinkParsesLosslesslyAndCoversEveryStatementFamily()
    {
        string path = Path.Combine(FindRepositoryRoot(), "samples", "KitchenSink", "Scripts", "main.vel");
        SourceText source = SourceText.ReadFrom(path);

        SyntaxTree tree = SyntaxTree.ParseText(source);
        SyntaxNode[] nodes = DescendantsAndSelf(tree.Root).ToArray();

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source.ToString(), tree.ToFullString());
        Assert.Contains(nodes, static node => node is CharacterDefinitionStatementSyntax);
        Assert.Contains(nodes, static node => node is ConfigStatementSyntax);
        Assert.Contains(nodes, static node => node is MetaStatementSyntax);
        Assert.Contains(nodes, static node => node is ScreenDefinitionStatementSyntax);
        Assert.Contains(nodes, static node => node is LabelStatementSyntax);
        Assert.Contains(nodes, static node => node is SceneStatementSyntax);
        Assert.Contains(nodes, static node => node is ShowStatementSyntax);
        Assert.Contains(nodes, static node => node is HideStatementSyntax);
        Assert.Contains(nodes, static node => node is DialogueStatementSyntax);
        Assert.Contains(nodes, static node => node is NarrationStatementSyntax);
        Assert.Contains(nodes, static node => node is PlayStatementSyntax);
        Assert.Contains(nodes, static node => node is StopStatementSyntax);
        Assert.Contains(nodes, static node => node is WaitStatementSyntax);
        Assert.Contains(nodes, static node => node is SetStatementSyntax);
        Assert.Contains(nodes, static node => node is JumpStatementSyntax);
        Assert.Contains(nodes, static node => node is MenuStatementSyntax);
        Assert.Contains(nodes, static node => node is IfStatementSyntax);
        Assert.Contains(nodes, static node => node is TweenStatementSyntax);
        Assert.Contains(nodes, static node => node is EmotionStatementSyntax);
        Assert.Contains(nodes, static node => node is InputStatementSyntax);
        Assert.Contains(nodes, static node => node is ParticleStatementSyntax);
        Assert.Contains(nodes, static node => node is ShowScreenStatementSyntax);
        Assert.Contains(nodes, static node => node is HideScreenStatementSyntax);
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;

        foreach (SyntaxNode child in node.ChildNodes())
        {
            foreach (SyntaxNode descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "samples", "KitchenSink", "game.velproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Velora repository root.");
    }
}
