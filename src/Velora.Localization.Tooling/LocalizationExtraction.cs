using Velora.Script.Syntax.Text;

namespace Velora.Localization.Tooling;

public sealed record ExtractedLocalizationEntry(
    string Key,
    string OriginalText,
    string Context,
    string Speaker,
    string SourceFile,
    TextSpan Span,
    int Line);

public enum LocalizationToolingDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record LocalizationToolingDiagnostic(
    string Id,
    LocalizationToolingDiagnosticSeverity Severity,
    string Message,
    string? Key = null,
    string? SourceFile = null,
    TextSpan? Span = null);

public sealed record LocalizationExtraction(
    IReadOnlyList<ExtractedLocalizationEntry> Entries,
    IReadOnlyList<LocalizationToolingDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(
        static diagnostic => diagnostic.Severity == LocalizationToolingDiagnosticSeverity.Error);
}
