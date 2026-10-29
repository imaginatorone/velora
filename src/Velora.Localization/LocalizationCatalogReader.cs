using System.Text.Json;

namespace Velora.Localization;

public enum LocalizationDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record LocalizationDiagnostic(
    string Id,
    LocalizationDiagnosticSeverity Severity,
    string Message,
    string? Key = null);

public sealed record LocalizationCatalogReadResult(
    LocalizationCatalog? Catalog,
    IReadOnlyList<LocalizationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Catalog is not null
        && Diagnostics.All(diagnostic => diagnostic.Severity != LocalizationDiagnosticSeverity.Error);
}

public static class LocalizationCatalogReader
{
    public const int MaximumDocumentBytes = 4 * 1024 * 1024;
    public const int MaximumEntries = 100_000;
    public const int MaximumKeyLength = 512;
    public const int MaximumTranslationLength = 1024 * 1024;

    public static LocalizationCatalogReadResult Read(ReadOnlyMemory<byte> utf8Json, LocaleCode fallbackLocale)
    {
        List<LocalizationDiagnostic> diagnostics = [];
        if (utf8Json.Length > MaximumDocumentBytes)
        {
            diagnostics.Add(Error("LOC001", $"The locale catalog exceeds {MaximumDocumentBytes} bytes."));
            return new LocalizationCatalogReadResult(null, diagnostics);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Error("LOC002", "A locale catalog root must be a JSON object."));
                return new LocalizationCatalogReadResult(null, diagnostics);
            }

            LocaleCode locale = fallbackLocale;
            string languageName = fallbackLocale.Value;
            string fontOverride = string.Empty;

            if (document.RootElement.TryGetProperty("__meta", out JsonElement metadata))
            {
                ReadMetadata(metadata, fallbackLocale, diagnostics, ref locale, ref languageName, ref fontOverride);
            }

            Dictionary<string, string> translations = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (translations.Count >= MaximumEntries && !translations.ContainsKey(property.Name))
                {
                    diagnostics.Add(Error("LOC003", $"The locale catalog exceeds {MaximumEntries} translations."));
                    return new LocalizationCatalogReadResult(null, diagnostics);
                }

                if (property.Name.Length is 0 or > MaximumKeyLength)
                {
                    diagnostics.Add(Warning("LOC004", "The translation key is empty or too long and was ignored.", property.Name));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    diagnostics.Add(Warning("LOC005", "A translation must be a JSON string and was ignored.", property.Name));
                    continue;
                }

                string translation = property.Value.GetString() ?? string.Empty;
                if (translation.Length > MaximumTranslationLength)
                {
                    diagnostics.Add(Warning("LOC006", "The translation is too long and was ignored.", property.Name));
                    continue;
                }

                translations[property.Name] = translation;
            }

            LocalizationCatalog catalog = new(locale, languageName, translations, fontOverride);
            return new LocalizationCatalogReadResult(catalog, diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("LOC007", $"Invalid locale catalog JSON: {exception.Message}"));
            return new LocalizationCatalogReadResult(null, diagnostics);
        }
    }

    private static void ReadMetadata(
        JsonElement metadata,
        LocaleCode fallbackLocale,
        List<LocalizationDiagnostic> diagnostics,
        ref LocaleCode locale,
        ref string languageName,
        ref string fontOverride)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Warning("LOC008", "The '__meta' value must be an object and was ignored.", "__meta"));
            return;
        }

        if (TryReadString(metadata, "language_code", out string? code))
        {
            if (LocaleCode.TryParse(code, out LocaleCode parsed))
            {
                locale = parsed;
            }
            else
            {
                diagnostics.Add(Warning(
                    "LOC009",
                    $"The metadata locale '{code}' is invalid; fallback '{fallbackLocale}' is used.",
                    "__meta.language_code"));
            }
        }

        if (TryReadString(metadata, "language_name", out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            languageName = name;
        }

        if (TryReadString(metadata, "font_override", out string? font))
        {
            fontOverride = font ?? string.Empty;
        }
    }

    private static bool TryReadString(JsonElement parent, string propertyName, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static LocalizationDiagnostic Error(string id, string message, string? key = null) =>
        new(id, LocalizationDiagnosticSeverity.Error, message, key);

    private static LocalizationDiagnostic Warning(string id, string message, string? key = null) =>
        new(id, LocalizationDiagnosticSeverity.Warning, message, key);
}
