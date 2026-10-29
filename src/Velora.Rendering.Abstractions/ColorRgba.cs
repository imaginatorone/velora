using System.Globalization;

namespace Velora.Rendering;

public readonly record struct ColorRgba(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    public static bool TryParseVelScript(string text, out ColorRgba color)
    {
        color = default;
        if (string.IsNullOrEmpty(text) || text[0] != '#')
        {
            return false;
        }

        ReadOnlySpan<char> digits = text.AsSpan(1);
        if (digits.Length == 3)
        {
            if (!TryNibble(digits[0], out byte r)
                || !TryNibble(digits[1], out byte g)
                || !TryNibble(digits[2], out byte b))
            {
                return false;
            }

            color = new ColorRgba((byte)((r << 4) | r), (byte)((g << 4) | g), (byte)((b << 4) | b));
            return true;
        }

        if (digits.Length is not (6 or 8)
            || !byte.TryParse(digits[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red)
            || !byte.TryParse(digits.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green)
            || !byte.TryParse(digits.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return false;
        }

        byte alpha = byte.MaxValue;
        if (digits.Length == 8
            && !byte.TryParse(digits.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out alpha))
        {
            return false;
        }

        color = new ColorRgba(red, green, blue, alpha);
        return true;
    }

    private static bool TryNibble(char value, out byte nibble)
    {
        if (value is >= '0' and <= '9')
        {
            nibble = (byte)(value - '0');
            return true;
        }

        if (value is >= 'a' and <= 'f')
        {
            nibble = (byte)(value - 'a' + 10);
            return true;
        }

        if (value is >= 'A' and <= 'F')
        {
            nibble = (byte)(value - 'A' + 10);
            return true;
        }

        nibble = 0;
        return false;
    }
}
