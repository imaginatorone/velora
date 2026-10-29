using System.Text;
using System.Text.Json;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Localization.Tooling.Tests;

public sealed class LocalizationToolingTests
{
    [Fact]
    public void ExtractsTypedAuthorVisibleTextIncludingNestedAndScreenContent()
    {
        const string source = """
            character mira = "Mira"
            screen notice:
                panel:
                    text "Restored"
                end
            end
            label start:
                mira "Hello"
                "Narration"
                menu:
                    "Continue":
                        if true:
                            mira "Nested"
                        end
                end
                input "Your name" -> player.name
                input "Route" -> route
                    "North"
                    "South"
                end
                meta set_title "Station Story"
            """;
        SyntaxTree tree = SyntaxTree.ParseText(source, "Scripts/main.vel");

        LocalizationExtraction extraction = LocalizationExtractor.Extract(tree);

        Assert.Empty(tree.Diagnostics);
        Assert.False(extraction.HasErrors);
        Assert.Equal(11, extraction.Entries.Count);
        Assert.Contains(extraction.Entries, entry => entry.Key == "character.mira.mira");
        Assert.Contains(extraction.Entries, entry => entry.Key == "screen.notice.restored");
        Assert.Contains(extraction.Entries, entry => entry.Key == "say.mira.hello");
        Assert.Contains(extraction.Entries, entry => entry.Key == "say.narration");
        Assert.Contains(extraction.Entries, entry => entry.Key == "menu.continue");
        Assert.Contains(extraction.Entries, entry => entry.Key == "say.mira.nested");
        Assert.Contains(extraction.Entries, entry => entry.Key == "input.your_name");
        Assert.Contains(extraction.Entries, entry => entry.Key == "input_option.north");
        Assert.Contains(extraction.Entries, entry => entry.Key == "title.station_story");
        Assert.All(extraction.Entries, entry => Assert.True(entry.Line > 0));
    }

    [Fact]
    public void ReportsGeneratedKeyCollisionsWithoutDroppingOccurrences()
    {
        SyntaxTree tree = SyntaxTree.ParseText("\"Hello!\"\n\"Hello?\"\n", "collision.vel");

        LocalizationExtraction extraction = LocalizationExtractor.Extract(tree);

        Assert.Equal(2, extraction.Entries.Count);
        LocalizationToolingDiagnostic diagnostic = Assert.Single(extraction.Diagnostics);
        Assert.Equal("LOCT001", diagnostic.Id);
        Assert.Equal(LocalizationToolingDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("say.hello", diagnostic.Key);
    }

    [Fact]
    public void CatalogValidationReportsMissingEmptyAndUnusedKeys()
    {
        LocalizationExtraction extraction = LocalizationExtractor.Extract(
            SyntaxTree.ParseText("mira \"Hello\"\n\"Narration\""));
        var catalog = new LocalizationCatalog(
            LocaleCode.Parse("ru"),
            "Russian",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["say.mira.hello"] = string.Empty,
                ["unused"] = "Лишнее",
            });

        IReadOnlyList<LocalizationToolingDiagnostic> diagnostics =
            LocalizationCatalogValidator.Validate(catalog, extraction);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOCT002" && diagnostic.Key == "say.narration");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOCT003" && diagnostic.Key == "say.mira.hello");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOCT004" && diagnostic.Key == "unused");
    }

    [Fact]
    public void TemplateArtifactsAreDeterministicValidUtf8JsonWithReferenceLocations()
    {
        LocalizationExtraction extraction = LocalizationExtractor.Extract(
            SyntaxTree.ParseText("mira \"Hello\"\nmira \"Hello\"\n", "Scripts/main.vel"));

        LocalizationTemplateFiles first = LocalizationTemplateWriter.Write(extraction);
        LocalizationTemplateFiles second = LocalizationTemplateWriter.Write(extraction);

        Assert.Equal(first.TemplateJson, second.TemplateJson);
        Assert.Equal(first.ReferenceJson, second.ReferenceJson);
        Assert.Equal(first.KeyMapJson, second.KeyMapJson);

        using JsonDocument template = JsonDocument.Parse(first.TemplateJson);
        Assert.Equal(string.Empty, template.RootElement.GetProperty("say.mira.hello").GetString());
        using JsonDocument keyMap = JsonDocument.Parse(first.KeyMapJson);
        JsonElement occurrences = keyMap.RootElement.GetProperty("say.mira.hello");
        Assert.Equal(2, occurrences.GetArrayLength());
        Assert.Equal("Scripts/main.vel", occurrences[0].GetProperty("file").GetString());
        Assert.Equal(1, occurrences[0].GetProperty("line").GetInt32());
        Assert.DoesNotContain(Encoding.UTF8.GetPreamble(), first.TemplateJson);
    }

    [Fact]
    public void ExtractionAcrossFilesIsOrderedByOrdinalProjectPath()
    {
        SyntaxTree second = SyntaxTree.ParseText("\"Second\"", "Scripts/z.vel");
        SyntaxTree first = SyntaxTree.ParseText("\"First\"", "Scripts/a.vel");

        LocalizationExtraction extraction = LocalizationExtractor.Extract([second, first]);

        Assert.Equal(
            ["Scripts/a.vel", "Scripts/z.vel"],
            extraction.Entries.Select(static entry => entry.SourceFile));
    }

    [Fact]
    public void KitchenSinkProducesACompleteCollisionFreeLocalizationTemplate()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "samples", "KitchenSink", "Scripts", "main.vel");
        SyntaxTree tree = SyntaxTree.ParseText(SourceText.ReadFrom(path));

        LocalizationExtraction extraction = LocalizationExtractor.Extract(tree);
        LocalizationTemplateFiles template = LocalizationTemplateWriter.Write(extraction);

        Assert.Empty(tree.Diagnostics);
        Assert.False(extraction.HasErrors);
        Assert.True(extraction.Entries.Count >= 20);
        Assert.Contains(extraction.Entries, static entry => entry.Context == "character" && entry.Speaker == "guide");
        Assert.Contains(extraction.Entries, static entry => entry.Context == "menu");
        Assert.Contains(extraction.Entries, static entry => entry.Context == "input_option");
        Assert.Contains(extraction.Entries, static entry => entry.Context == "screen" && entry.Speaker == "status_panel");
        Assert.Contains(extraction.Entries, static entry => entry.Context == "title");
        using JsonDocument json = JsonDocument.Parse(template.TemplateJson);
        Assert.True(json.RootElement.TryGetProperty("__meta", out _));
        Assert.Equal(extraction.Entries.Select(static entry => entry.Key).Distinct(StringComparer.Ordinal).Count(),
            json.RootElement.EnumerateObject().Count(static property => property.Name != "__meta"));
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
