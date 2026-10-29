namespace Velora.Localization.Tooling;

public static class LocalizationCatalogValidator
{
    public static IReadOnlyList<LocalizationToolingDiagnostic> Validate(
        LocalizationCatalog catalog,
        LocalizationExtraction extraction)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(extraction);

        List<LocalizationToolingDiagnostic> diagnostics = [.. extraction.Diagnostics];
        Dictionary<string, ExtractedLocalizationEntry> required = extraction.Entries
            .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        foreach ((string key, ExtractedLocalizationEntry entry) in required.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (!catalog.TryGetTranslation(key, out string translation))
            {
                diagnostics.Add(new LocalizationToolingDiagnostic(
                    "LOCT002",
                    LocalizationToolingDiagnosticSeverity.Warning,
                    $"Locale '{catalog.Locale}' is missing key '{key}'.",
                    key,
                    entry.SourceFile,
                    entry.Span));
            }
            else if (translation.Length == 0)
            {
                diagnostics.Add(new LocalizationToolingDiagnostic(
                    "LOCT003",
                    LocalizationToolingDiagnosticSeverity.Warning,
                    $"Locale '{catalog.Locale}' has an empty translation for '{key}'.",
                    key,
                    entry.SourceFile,
                    entry.Span));
            }
        }

        foreach (string key in catalog.Translations.Keys
                     .Where(key => !required.ContainsKey(key))
                     .Order(StringComparer.Ordinal))
        {
            diagnostics.Add(new LocalizationToolingDiagnostic(
                "LOCT004",
                LocalizationToolingDiagnosticSeverity.Warning,
                $"Locale '{catalog.Locale}' contains unused key '{key}'.",
                key));
        }

        return diagnostics;
    }
}
