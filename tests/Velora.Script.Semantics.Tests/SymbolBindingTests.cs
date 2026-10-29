using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Semantics.Tests;

public sealed class SymbolBindingTests
{
    [Fact]
    public void BundledExampleBindsWithoutSemanticDiagnostics()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "assets", "scripts", "example.vel");
        SyntaxTree tree = SyntaxTree.ParseText(SourceText.ReadFrom(path));

        SemanticModel model = SemanticModel.Create(tree);

        Assert.Empty(tree.Diagnostics);
        Assert.Empty(model.SemanticDiagnostics);
        Assert.All(model.References, static reference => Assert.True(reference.IsResolved));
    }

    [Fact]
    public void CreatesSymbolsForCurrentLanguageDeclarations()
    {
        const string source = """
            character mira = "Mira"
            set player.name = "Alex"
            input "Route" -> route
            config ui.theme = "dark"
            screen notice:
                text "Notice"
            end
            label start:
            """;

        SemanticModel model = Analyze(source);

        Assert.NotNull(model.LookupSymbol(SymbolKind.Character, "mira"));
        Assert.NotNull(model.LookupSymbol(SymbolKind.Label, "start"));
        Assert.NotNull(model.LookupSymbol(SymbolKind.Screen, "notice"));
        Assert.NotNull(model.LookupSymbol(SymbolKind.Variable, "player.name"));
        Assert.NotNull(model.LookupSymbol(SymbolKind.Variable, "route"));
        Assert.NotNull(model.LookupSymbol(SymbolKind.Variable, "__config.ui.theme"));
        Assert.Empty(model.SemanticDiagnostics);
    }

    [Fact]
    public void BindsEveryDeclarationSyntaxToItsSymbol()
    {
        const string source = """
            character mira = "Mira"
            set score = 1
            input "Name" -> player.name
            config locale = "en"
            screen overlay:
            end
            label start:
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);

        foreach (StatementSyntax statement in tree.Root.Statements)
        {
            Assert.NotNull(model.GetDeclaredSymbol(statement));
        }
    }

    [Fact]
    public void RepeatedVariableWritesShareOneSymbolWithoutDuplicateDiagnostic()
    {
        const string source = """
            set score = 0
            set score = score + 1
            input "Correct it" -> score
            """;

        SemanticModel model = Analyze(source);
        Symbol score = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Variable, "score"));

        Assert.Equal(3, score.Definitions.Count);
        Assert.Equal(SymbolDefinitionKind.InputTarget, score.EffectiveDefinition?.Kind);
        Assert.DoesNotContain(
            model.SemanticDiagnostics,
            static diagnostic => diagnostic.Id is SemanticDiagnosticIds.DuplicateLabel or
                SemanticDiagnosticIds.DuplicateCharacter or SemanticDiagnosticIds.DuplicateScreen);
        Assert.Single(model.FindReferences(score));
    }

    [Fact]
    public void RuntimeBuiltinsAreImplicitResolvedVariableSymbols()
    {
        const string source = "set summary = os_username + os_time + os_locale + sys.os_username + sys.name_gibberish";

        SemanticModel model = Analyze(source);

        Assert.DoesNotContain(
            model.SemanticDiagnostics,
            static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedVariable);
        Assert.All(
            model.GetSymbols(SymbolKind.Variable).Where(static symbol => symbol.Name.StartsWith("os_", StringComparison.Ordinal)),
            static symbol => Assert.True(symbol.IsImplicit));
        Assert.Equal(5, model.References.Count(static reference => reference.TargetKind == SymbolKind.Variable));
    }

    [Fact]
    public void ForwardReferencesResolveAcrossTheWholeProgram()
    {
        const string source = """
            future "Value: [later]"
            jump ending
            show_screen credits
            set copy = later
            character future = "Future"
            set later = 42
            screen credits:
            end
            label ending:
            """;

        SemanticModel model = Analyze(source);

        Assert.All(model.References, static reference => Assert.True(reference.IsResolved));
        Assert.Empty(model.SemanticDiagnostics);
    }

    [Fact]
    public void NameSpacesAreCaseSensitiveAndIndependent()
    {
        const string source = """
            character Same = "Speaker"
            screen Same:
            end
            label Same:
            same "Unknown because identifiers are case-sensitive."
            """;

        SemanticModel model = Analyze(source);

        Assert.Equal(3, model.LookupSymbols("Same").Count);
        Assert.Null(model.LookupSymbol(SymbolKind.Character, "same"));
        Assert.Contains(
            model.SemanticDiagnostics,
            static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedCharacter);
    }

    private static SemanticModel Analyze(string source) => SemanticModel.Create(SyntaxTree.ParseText(source));

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
