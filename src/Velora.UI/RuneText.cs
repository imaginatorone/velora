using System.Text;

namespace Velora.UI;

public static class RuneText
{
    public static string Take(string text, int runeCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(runeCount);
        if (runeCount == 0 || text.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(Math.Min(text.Length, runeCount));
        int remaining = runeCount;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (remaining-- == 0)
            {
                break;
            }

            builder.Append(rune);
        }

        return builder.ToString();
    }

    public static string Truncate(string text, int maxRunes, string suffix = "…")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(suffix);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRunes);

        int count = 0;
        foreach (Rune _ in text.EnumerateRunes())
        {
            count++;
            if (count > maxRunes)
            {
                return Take(text, maxRunes) + suffix;
            }
        }

        return text;
    }
}
