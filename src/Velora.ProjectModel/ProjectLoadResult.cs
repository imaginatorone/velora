namespace Velora.ProjectModel;

/// <summary>The validated project, or diagnostics explaining why it was rejected.</summary>
public sealed record ProjectLoadResult(
    VeloraProject? Project,
    IReadOnlyList<ProjectDiagnostic> Diagnostics)
{
    public bool IsSuccess => Project is not null &&
        !Diagnostics.Any(static diagnostic => diagnostic.Severity == ProjectDiagnosticSeverity.Error);
}
