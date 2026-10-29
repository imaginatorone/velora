namespace Velora.ProjectModel;

/// <summary>A user-facing problem found while loading project metadata.</summary>
public sealed record ProjectDiagnostic(
    string Id,
    ProjectDiagnosticSeverity Severity,
    string Message,
    string? Source = null);
