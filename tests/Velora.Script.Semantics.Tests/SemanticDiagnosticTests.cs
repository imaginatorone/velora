using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Diagnostics;

namespace Velora.Script.Semantics.Tests;

public sealed class SemanticDiagnosticTests
{
    [Fact]
    public void DuplicateDeclarationsWarnAndUseLastDefinition()
    {
        const string source = """
            character mira = "First"
            character mira = "Second"
            screen notice:
            end
            screen notice:
            end
            label route:
            label route:
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);

        Assert.Equal(
            [
                SemanticDiagnosticIds.DuplicateCharacter,
                SemanticDiagnosticIds.DuplicateScreen,
                SemanticDiagnosticIds.DuplicateLabel,
            ],
            model.SemanticDiagnostics.Select(static diagnostic => diagnostic.Id));
        Assert.All(model.SemanticDiagnostics, static diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));

        Symbol character = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Character, "mira"));
        Symbol screen = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Screen, "notice"));
        Symbol label = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Label, "route"));
        Assert.Same(tree.Root.Statements[1], character.EffectiveDefinition?.Syntax);
        Assert.Same(tree.Root.Statements[3], screen.EffectiveDefinition?.Syntax);
        Assert.Same(tree.Root.Statements[5], label.EffectiveDefinition?.Syntax);
        Assert.Equal(2, character.Definitions.Count);
        Assert.Equal(2, screen.Definitions.Count);
        Assert.Equal(2, label.Definitions.Count);
    }

    [Fact]
    public void UnknownReferencesProduceNonBlockingDiagnosticsAndReferences()
    {
        const string source = """
            ghost "[missing]"
            emotion specter angry
            jump nowhere
            show_screen absent
            hide_screen absent
            set answer = missing.value
            """;

        SemanticModel model = Analyze(source);

        Assert.Equal(7, model.SemanticDiagnostics.Count);
        Assert.All(model.SemanticDiagnostics, static diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.All(model.References, static reference => Assert.False(reference.IsResolved));
        Assert.Equal(2, model.SemanticDiagnostics.Count(static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedCharacter));
        Assert.Equal(2, model.SemanticDiagnostics.Count(static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedScreen));
        Assert.Equal(2, model.SemanticDiagnostics.Count(static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedVariable));
        Assert.Single(model.SemanticDiagnostics, static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedLabel);
    }

    [Fact]
    public void UnknownScreenActionJumpIsBoundAsALabelReference()
    {
        const string source = """
            screen notice:
                imagebutton "ok.png" action jump missing
            end
            """;

        SemanticModel model = Analyze(source);
        SemanticReference reference = Assert.Single(model.References);

        Assert.Equal(SemanticReferenceKind.ScreenActionJump, reference.Kind);
        Assert.Equal(SymbolKind.Label, reference.TargetKind);
        Assert.Equal("missing", reference.Name);
        Assert.False(reference.IsResolved);
        Assert.Equal(SemanticDiagnosticIds.UndefinedLabel, Assert.Single(model.SemanticDiagnostics).Id);
    }

    [Fact]
    public void NestedLabelDoesNotBecomeLegacyJumpTarget()
    {
        const string source = """
            if true:
                label hidden:
            end
            jump hidden
            """;

        SemanticModel model = Analyze(source);

        Assert.Null(model.LookupSymbol(SymbolKind.Label, "hidden"));
        Assert.Contains(model.SemanticDiagnostics, static diagnostic => diagnostic.Id == SemanticDiagnosticIds.NestedLabel);
        Assert.Contains(model.SemanticDiagnostics, static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedLabel);
    }

    [Fact]
    public void MissingParserTokensDoNotCauseCascadingEmptyNameDiagnostics()
    {
        const string source = """
            character =
            jump
            show_screen
            """;

        SemanticModel model = Analyze(source);

        Assert.NotEmpty(model.SyntaxTree.Diagnostics);
        Assert.Empty(model.SemanticDiagnostics);
        Assert.DoesNotContain(model.Symbols, static symbol => symbol.Name.Length == 0);
        Assert.Empty(model.References);
    }

    [Fact]
    public void SemanticModelCoexistsWithMalformedParseAndCombinesDiagnostics()
    {
        const string source = """
            set = missing
            if true:
                jump absent
            end
            label recovered:
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);

        SemanticModel? model = null;
        Exception? exception = Record.Exception(() => model = SemanticModel.Create(tree));

        Assert.Null(exception);
        Assert.NotNull(model);
        Assert.NotEmpty(model.SyntaxTree.Diagnostics);
        Assert.Contains(model.Diagnostics, static diagnostic => diagnostic.Id.StartsWith("PAR", StringComparison.Ordinal));
        Assert.Contains(model.Diagnostics, static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedVariable);
        Assert.Contains(model.Diagnostics, static diagnostic => diagnostic.Id == SemanticDiagnosticIds.UndefinedLabel);
        Assert.NotNull(model.LookupSymbol(SymbolKind.Label, "recovered"));
    }

    [Fact]
    public void DiagnosticsAreDeterministicByUtf16SpanThenId()
    {
        const string source = """
            ghost "🙂 [missing]"
            jump absent
            """;

        SemanticModel first = Analyze(source);
        SemanticModel second = Analyze(source);

        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(
            first.Diagnostics.OrderBy(static diagnostic => diagnostic.Span.Start).ThenBy(static diagnostic => diagnostic.Span.Length),
            first.Diagnostics);
    }

    private static SemanticModel Analyze(string source) => SemanticModel.Create(SyntaxTree.ParseText(source));
}
