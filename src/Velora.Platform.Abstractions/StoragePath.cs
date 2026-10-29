namespace Velora.Platform;

/// <summary>
/// A normalized relative storage name. This is a logical identifier only;
/// host implementations must additionally prove canonical containment of the
/// final filesystem target before every operation.
/// </summary>
public readonly record struct StoragePath
{
    public const int MaximumLength = 512;

    private readonly string? _value;

    private StoragePath(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public bool IsValid => _value is not null;

    public static StoragePath Parse(string value)
    {
        if (!TryParse(value, out StoragePath path))
        {
            throw new FormatException($"'{value}' is not a safe relative storage path.");
        }

        return path;
    }

    public static bool TryParse(string? value, out StoragePath path)
    {
        path = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Contains('\0'))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        if (normalized[0] == '/'
            || normalized[^1] == '/'
            || normalized.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            return false;
        }

        path = new StoragePath(normalized);
        return true;
    }

    public override string ToString() => Value;
}
