using System.Text;
using Velora.Localization;

namespace Velora.Localization.Tests;

public sealed class LocalizationCatalogReaderTests
{
    [Fact]
    public void ReadsBundledEnglishCatalogWithoutReflectionModels()
    {
        string repositoryRoot = FindRepositoryRoot();
        byte[] json = File.ReadAllBytes(Path.Combine(repositoryRoot, "assets", "locales", "en.json"));

        LocalizationCatalogReadResult result = LocalizationCatalogReader.Read(json, LocaleCode.Parse("en"));

        Assert.True(result.IsSuccess);
        LocalizationCatalog catalog = Assert.IsType<LocalizationCatalog>(result.Catalog);
        Assert.Equal("en", catalog.Locale.Value);
        Assert.Equal("English", catalog.LanguageName);
        Assert.Equal("New Game", catalog.Translations["menu.new_game"]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void IgnoresNonStringEntriesWithDiagnosticAndUsesLastDuplicate()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            """
            {
              "__meta": { "language_code": "ru", "language_name": "Russian" },
              "say.mira.hello": "first",
              "invalid": 42,
              "say.mira.hello": "second"
            }
            """);

        LocalizationCatalogReadResult result = LocalizationCatalogReader.Read(json, LocaleCode.Parse("en"));

        Assert.True(result.IsSuccess);
        Assert.Equal("second", result.Catalog!.Translations["say.mira.hello"]);
        LocalizationDiagnostic warning = Assert.Single(result.Diagnostics);
        Assert.Equal("LOC005", warning.Id);
        Assert.Equal(LocalizationDiagnosticSeverity.Warning, warning.Severity);
    }

    [Theory]
    [InlineData("[]", "LOC002")]
    [InlineData("{", "LOC007")]
    public void RejectsMalformedCatalogs(string json, string expectedDiagnostic)
    {
        LocalizationCatalogReadResult result = LocalizationCatalogReader.Read(
            Encoding.UTF8.GetBytes(json),
            LocaleCode.Parse("en"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expectedDiagnostic);
    }

    [Fact]
    public void RejectsOversizedDocumentBeforeParsing()
    {
        byte[] json = new byte[LocalizationCatalogReader.MaximumDocumentBytes + 1];

        LocalizationCatalogReadResult result = LocalizationCatalogReader.Read(json, LocaleCode.Parse("en"));

        Assert.False(result.IsSuccess);
        Assert.Equal("LOC001", Assert.Single(result.Diagnostics).Id);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "locales", "en.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Velora repository root.");
    }
}
