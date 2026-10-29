namespace Velora.Assets.Diagnostics;

/// <summary>A path, resolution, or database problem associated with an asset.</summary>
public sealed record AssetDiagnostic(
    string Id,
    AssetDiagnosticSeverity Severity,
    string Message,
    string? LogicalPath = null,
    string? SuggestedLogicalPath = null);
