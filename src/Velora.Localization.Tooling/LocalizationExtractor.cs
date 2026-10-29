using Velora.Script.Syntax;

namespace Velora.Localization.Tooling;

/// <summary>Extracts author-visible text from the typed, lossless syntax tree.</summary>
public static class LocalizationExtractor
{
    public static LocalizationExtraction Extract(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        List<ExtractedLocalizationEntry> entries = [];
        ExtractStatements(tree, tree.Root.Statements, entries);
        return BuildResult(entries);
    }

    public static LocalizationExtraction Extract(IEnumerable<SyntaxTree> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);

        List<ExtractedLocalizationEntry> entries = [];
        foreach (SyntaxTree tree in trees
                     .OrderBy(static item => item.Text.FilePath, StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(tree);
            ExtractStatements(tree, tree.Root.Statements, entries);
        }

        return BuildResult(entries);
    }

    private static LocalizationExtraction BuildResult(List<ExtractedLocalizationEntry> entries)
    {
        List<LocalizationToolingDiagnostic> diagnostics = [];
        foreach (IGrouping<string, ExtractedLocalizationEntry> group in entries
                     .GroupBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            string[] originals = group
                .Select(static entry => entry.OriginalText)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (originals.Length <= 1)
            {
                continue;
            }

            ExtractedLocalizationEntry first = group.First();
            diagnostics.Add(new LocalizationToolingDiagnostic(
                "LOCT001",
                LocalizationToolingDiagnosticSeverity.Error,
                $"Generated localization key '{group.Key}' represents multiple source strings.",
                group.Key,
                first.SourceFile,
                first.Span));
        }

        return new LocalizationExtraction(entries.ToArray(), diagnostics.ToArray());
    }

    private static void ExtractStatements(
        SyntaxTree tree,
        IEnumerable<StatementSyntax> statements,
        List<ExtractedLocalizationEntry> entries)
    {
        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case CharacterDefinitionStatementSyntax character:
                    Add(tree, entries, character.DisplayNameToken, "character", character.Identifier.ValueText);
                    break;
                case DialogueStatementSyntax dialogue:
                    Add(tree, entries, dialogue.TextToken, "say", dialogue.SpeakerToken.ValueText);
                    break;
                case NarrationStatementSyntax narration:
                    Add(tree, entries, narration.TextToken, "say", string.Empty);
                    break;
                case MenuStatementSyntax menu:
                    foreach (ChoiceClauseSyntax choice in menu.Choices)
                    {
                        Add(tree, entries, choice.TextToken, "menu", string.Empty);
                        ExtractStatements(tree, choice.Statements, entries);
                    }

                    break;
                case IfStatementSyntax conditional:
                    ExtractStatements(tree, conditional.ThenStatements, entries);
                    foreach (ElifClauseSyntax clause in conditional.ElifClauses)
                    {
                        ExtractStatements(tree, clause.Statements, entries);
                    }

                    if (conditional.ElseClause is not null)
                    {
                        ExtractStatements(tree, conditional.ElseClause.Statements, entries);
                    }

                    break;
                case InputStatementSyntax input:
                    Add(tree, entries, input.PromptToken, "input", string.Empty);
                    foreach (InputOptionSyntax option in input.Options)
                    {
                        Add(tree, entries, option.TextToken, "input_option", string.Empty);
                    }

                    break;
                case ScreenDefinitionStatementSyntax screen:
                    ExtractScreenElements(tree, screen.NameToken.ValueText, screen.Elements, entries);
                    break;
                case MetaStatementSyntax meta when
                    meta.ActionToken.ValueText == "set_title" &&
                    meta.Arguments.Count > 0 &&
                    meta.Arguments[0].Kind == SyntaxKind.StringLiteralToken:
                    Add(tree, entries, meta.Arguments[0], "title", string.Empty);
                    break;
            }
        }
    }

    private static void ExtractScreenElements(
        SyntaxTree tree,
        string screenName,
        IEnumerable<ScreenElementSyntax> elements,
        List<ExtractedLocalizationEntry> entries)
    {
        foreach (ScreenElementSyntax element in elements)
        {
            switch (element)
            {
                case TextScreenElementSyntax text:
                    Add(tree, entries, text.TextToken, "screen", screenName);
                    break;
                case PanelScreenElementSyntax panel:
                    ExtractScreenElements(tree, screenName, panel.Children, entries);
                    break;
            }
        }
    }

    private static void Add(
        SyntaxTree tree,
        List<ExtractedLocalizationEntry> entries,
        SyntaxToken token,
        string context,
        string speaker)
    {
        if (token.IsMissing || token.ValueText.Length == 0)
        {
            return;
        }

        string original = token.ValueText;
        entries.Add(new ExtractedLocalizationEntry(
            LocalizationKey.Generate(original, context, speaker),
            original,
            context,
            speaker,
            tree.Text.FilePath,
            token.Span,
            tree.Text.GetLinePosition(token.Span.Start).Line + 1));
    }
}
