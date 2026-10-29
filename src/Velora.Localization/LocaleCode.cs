namespace Velora.Localization;

/// <summary>
/// A filesystem-neutral locale identifier accepted by Velora catalogs.
/// </summary>
public readonly record struct LocaleCode
{
    public const int MaximumLength = 64;

    private readonly string? _value;

    private LocaleCode(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public bool IsValid => _value is not null;

    public static LocaleCode Parse(string value)
    {
        if (!TryParse(value, out LocaleCode locale))
        {
            throw new FormatException($"'{value}' is not a valid Velora locale code.");
        }

        return locale;
    }

    public static bool TryParse(string? value, out LocaleCode locale)
    {
        locale = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        int segmentLength = 0;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            bool isAsciiLetterOrDigit = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9';
            if (!isAsciiLetterOrDigit)
            {
                return false;
            }

            segmentLength++;
        }

        if (segmentLength == 0)
        {
            return false;
        }

        locale = new LocaleCode(value);
        return true;
    }

    public override string ToString() => Value;
}
