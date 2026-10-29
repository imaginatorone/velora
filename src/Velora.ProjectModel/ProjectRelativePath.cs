namespace Velora.ProjectModel;

/// <summary>
/// A normalized, case-sensitive path relative to a Velora project directory.
/// Its portable representation always uses forward slashes.
/// </summary>
public readonly struct ProjectRelativePath : IEquatable<ProjectRelativePath>
{
    public const int MaximumLength = 1024;

    private readonly string? _value;

    private ProjectRelativePath(string value)
    {
        _value = value;
    }

    /// <summary>Gets the normalized forward-slash representation.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Gets whether this value was created by <see cref="TryCreate"/>.</summary>
    public bool IsValid => _value is not null;

    public static bool TryCreate(
        string? value,
        out ProjectRelativePath path,
        out ProjectPathError error)
    {
        path = default;
        error = ProjectPathError.None;

        if (string.IsNullOrEmpty(value))
        {
            error = ProjectPathError.Empty;
            return false;
        }

        if (value.Length > MaximumLength)
        {
            error = ProjectPathError.TooLong;
            return false;
        }

        var portable = value.Replace('\\', '/');
        if (portable[0] == '/' || IsDriveQualified(portable))
        {
            error = ProjectPathError.Rooted;
            return false;
        }

        var components = new List<string>();
        foreach (var component in portable.Split('/', StringSplitOptions.None))
        {
            if (component.Length == 0 || component == ".")
            {
                continue;
            }

            if (component == "..")
            {
                if (components.Count == 0)
                {
                    error = ProjectPathError.EscapesRoot;
                    return false;
                }

                components.RemoveAt(components.Count - 1);
                continue;
            }

            if (ContainsInvalidCharacter(component))
            {
                error = ProjectPathError.InvalidCharacter;
                return false;
            }

            components.Add(component);
        }

        if (components.Count == 0)
        {
            error = ProjectPathError.Empty;
            return false;
        }

        var normalized = string.Join('/', components);
        if (normalized.Length > MaximumLength)
        {
            error = ProjectPathError.TooLong;
            return false;
        }

        path = new ProjectRelativePath(normalized);
        return true;
    }

    public bool Equals(ProjectRelativePath other) =>
        StringComparer.Ordinal.Equals(Value, other.Value) && IsValid == other.IsValid;

    public override bool Equals(object? obj) => obj is ProjectRelativePath other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsValid, StringComparer.Ordinal.GetHashCode(Value));

    public override string ToString() => Value;

    public static bool operator ==(ProjectRelativePath left, ProjectRelativePath right) => left.Equals(right);

    public static bool operator !=(ProjectRelativePath left, ProjectRelativePath right) => !left.Equals(right);

    private static bool IsDriveQualified(string value) =>
        value.Length >= 2 && IsAsciiLetter(value[0]) && value[1] == ':';

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool ContainsInvalidCharacter(string component)
    {
        foreach (var value in component)
        {
            if (value == ':' || char.IsControl(value))
            {
                return true;
            }
        }

        return false;
    }
}
