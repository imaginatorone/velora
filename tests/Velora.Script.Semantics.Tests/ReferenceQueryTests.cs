using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Semantics.Tests;

public sealed class ReferenceQueryTests
{
    [Fact]
    public void FindsDefinitionsReferencesAndRenameSpans()
    {
        const string source = """
            set player.name = "Alex"
            set copy = player.name
            "Hello, [player.name]."
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);
        Symbol playerName = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Variable, "player.name"));

        Assert.Single(playerName.Definitions);
        Assert.Equal(2, model.FindReferences(playerName).Count);
        Assert.Equal(
            ["player.name", "player.name", "player.name"],
            model.GetRenameSpans(playerName).Select(tree.Text.GetText));
    }

    [Fact]
    public void GetsDeclaredAndReferencedSymbolsFromSyntaxNodes()
    {
        const string source = """
            character mira = "Mira"
            set player.name = "Alex"
            mira "Hello, [player.name]."
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);
        CharacterDefinitionStatementSyntax character = Assert.IsType<CharacterDefinitionStatementSyntax>(tree.Root.Statements[0]);
        DialogueStatementSyntax dialogue = Assert.IsType<DialogueStatementSyntax>(tree.Root.Statements[2]);

        Symbol declared = Assert.IsType<Symbol>(model.GetDeclaredSymbol(character));
        IReadOnlyList<SemanticReference> references = model.GetReferences(dialogue);

        Assert.Equal(SymbolKind.Character, declared.Kind);
        Assert.Equal(2, references.Count);
        Assert.Collection(
            references,
            reference => Assert.Same(declared, reference.Symbol),
            reference => Assert.Equal("player.name", reference.Symbol?.Name));
    }

    [Fact]
    public void GetsResolvedSymbolAtDefinitionAndReferencePositions()
    {
        const string source = "set visits = 0\nset next = visits + 1";
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);
        int definitionPosition = source.IndexOf("visits", StringComparison.Ordinal);
        int referencePosition = source.LastIndexOf("visits", StringComparison.Ordinal) + 2;

        Symbol? fromDefinition = model.GetSymbolAtPosition(definitionPosition);
        Symbol? fromReference = model.GetSymbolAtPosition(referencePosition);

        Assert.NotNull(fromDefinition);
        Assert.Same(fromDefinition, fromReference);
        Assert.Equal("visits", fromDefinition.Name);
    }

    [Fact]
    public void InterpolationSpanUsesUtf16CoordinatesAfterSupplementaryCharacter()
    {
        const string source = """
            character mira = "Mira"
            set player.name = "Alex"
            mira "🙂 [player.name]"
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);
        SemanticReference interpolation = Assert.Single(
            model.References,
            static reference => reference.Kind == SemanticReferenceKind.VariableInterpolation);
        int expectedStart = source.LastIndexOf("player.name", StringComparison.Ordinal);

        Assert.Equal(new TextSpan(expectedStart, "player.name".Length), interpolation.Span);
        Assert.Equal("player.name", tree.Text.GetText(interpolation.Span));
    }

    [Fact]
    public void InterpolationScannerPreservesRawSpanAcrossLegacyEscapes()
    {
        const string source = "set name = \"Alex\"\n\"\\[name]\"";
        SyntaxTree tree = SyntaxTree.ParseText(source);
        SemanticModel model = SemanticModel.Create(tree);
        SemanticReference interpolation = Assert.Single(
            model.References,
            static reference => reference.Kind == SemanticReferenceKind.VariableInterpolation);

        Assert.Equal("name", interpolation.Name);
        Assert.Equal("name", tree.Text.GetText(interpolation.Span));
    }

    [Fact]
    public void UnresolvedReferenceRemainsQueryableAtItsSourceSpan()
    {
        const string source = "jump missing";
        SemanticModel model = SemanticModel.Create(SyntaxTree.ParseText(source));
        SemanticReference reference = Assert.Single(model.References);

        Assert.Null(reference.Symbol);
        Assert.False(reference.IsResolved);
        Assert.Null(model.GetSymbolAtPosition(reference.Span.Start));
    }

    [Fact]
    public void ScreenActionAndStatementJumpsFindTheSameLabelSymbol()
    {
        const string source = """
            screen notice:
                imagebutton "ok.png" action jump finish
            end
            show_screen notice
            jump finish
            label finish:
            """;
        SemanticModel model = Analyze(source);
        Symbol finish = Assert.IsType<Symbol>(model.LookupSymbol(SymbolKind.Label, "finish"));

        Assert.Equal(2, model.FindReferences(finish).Count);
        Assert.Contains(model.FindReferences(finish), static reference => reference.Kind == SemanticReferenceKind.ScreenActionJump);
        Assert.Contains(model.FindReferences(finish), static reference => reference.Kind == SemanticReferenceKind.Jump);
        Assert.Empty(model.SemanticDiagnostics);
    }

    [Fact]
    public void ReferenceAndSymbolEnumerationIsDeterministic()
    {
        const string source = """
            set zeta = 1
            label beta:
            character alpha = "A"
            screen omega:
            end
            set alpha = zeta
            """;
        SemanticModel first = Analyze(source);
        SemanticModel second = Analyze(source);

        Assert.Equal(
            first.Symbols.Select(static symbol => (symbol.Kind, symbol.Name)),
            second.Symbols.Select(static symbol => (symbol.Kind, symbol.Name)));
        Assert.Equal(
            first.References.Select(static reference =>
                (reference.Name, reference.TargetKind, reference.Kind, reference.Span, reference.IsResolved)),
            second.References.Select(static reference =>
                (reference.Name, reference.TargetKind, reference.Kind, reference.Span, reference.IsResolved)));
    }

    private static SemanticModel Analyze(string source) => SemanticModel.Create(SyntaxTree.ParseText(source));
}
