namespace Velora.ProjectModel;

/// <summary>Describes why a project-relative path could not be normalized.</summary>
public enum ProjectPathError
{
    None,
    Empty,
    TooLong,
    Rooted,
    EscapesRoot,
    InvalidCharacter,
}
