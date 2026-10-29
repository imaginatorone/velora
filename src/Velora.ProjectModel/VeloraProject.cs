namespace Velora.ProjectModel;

/// <summary>Represents the validated contents of a version 1 <c>.velproj</c> file.</summary>
public sealed record VeloraProject(
    int FormatVersion,
    string Name,
    ProjectRelativePath EntryScript,
    ProjectRelativePath AssetsDirectory)
{
    public const int CurrentFormatVersion = 1;
}
