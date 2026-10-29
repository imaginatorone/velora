namespace Velora.Assets;

/// <summary>
/// A normalized, case-sensitive asset identifier. The portable representation
/// is relative and always uses forward slashes.
/// </summary>
public readonly struct AssetId : IEquatable<AssetId>, IComparable<AssetId>
{
    public const int MaximumLength = 1024;

    private readonly string? _value;

    private AssetId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public bool IsValid => _value is not null;

    public static bool TryCreate(string? value, out AssetId assetId, out AssetPathError error)
    {
        assetId = default;
        error = AssetPathError.None;

        if (string.IsNullOrEmpty(value))
        {
            error = AssetPathError.Empty;
            return false;
        }

        if (value.Length > MaximumLength)
        {
            error = AssetPathError.TooLong;
            return false;
        }

        var portable = value.Replace('\\', '/');
        if (portable[0] == '/' || IsDriveQualified(portable))
        {
            error = AssetPathError.Rooted;
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
                    error = AssetPathError.EscapesRoot;
                    return false;
                }

                components.RemoveAt(components.Count - 1);
                continue;
            }

            if (ContainsInvalidCharacter(component))
            {
                error = AssetPathError.InvalidCharacter;
                return false;
            }

            components.Add(component);
        }

        if (components.Count == 0)
        {
            error = AssetPathError.Empty;
            return false;
        }

        var normalized = string.Join('/', components);
        if (normalized.Length > MaximumLength)
        {
            error = AssetPathError.TooLong;
            return false;
        }

        assetId = new AssetId(normalized);
        return true;
    }

    public static AssetId Parse(string value)
    {
        if (!TryCreate(value, out var assetId, out var error))
        {
            throw new FormatException($"Invalid logical asset identifier ({error}).");
        }

        return assetId;
    }

    public bool Equals(AssetId other) =>
        StringComparer.Ordinal.Equals(Value, other.Value) && IsValid == other.IsValid;

    public override bool Equals(object? obj) => obj is AssetId other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsValid, StringComparer.Ordinal.GetHashCode(Value));

    public int CompareTo(AssetId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;

    public static bool operator ==(AssetId left, AssetId right) => left.Equals(right);

    public static bool operator !=(AssetId left, AssetId right) => !left.Equals(right);

    public static bool operator <(AssetId left, AssetId right) => left.CompareTo(right) < 0;

    public static bool operator <=(AssetId left, AssetId right) => left.CompareTo(right) <= 0;

    public static bool operator >(AssetId left, AssetId right) => left.CompareTo(right) > 0;

    public static bool operator >=(AssetId left, AssetId right) => left.CompareTo(right) >= 0;

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
