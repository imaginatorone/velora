using System.Text;

namespace Velora.Runtime;

internal static class NameHeuristics
{
    internal static bool IsGibberish(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return false;
        }

        Rune[] codePoints = [.. text.EnumerateRunes()];
        int maximumConsonants = 0;
        int currentConsonants = 0;
        int maximumRepeated = 1;
        int currentRepeated = 1;
        int letterCount = 0;
        bool hasVowel = false;

        for (int index = 0; index < codePoints.Length; index++)
        {
            int codePoint = ToLegacyLower(codePoints[index].Value);
            if (IsLegacyLetter(codePoint))
            {
                letterCount++;
                if (IsLegacyVowel(codePoint))
                {
                    hasVowel = true;
                    currentConsonants = 0;
                }
                else
                {
                    currentConsonants++;
                    maximumConsonants = Math.Max(maximumConsonants, currentConsonants);
                }
            }

            if (index > 0 && codePoint == ToLegacyLower(codePoints[index - 1].Value))
            {
                currentRepeated++;
                maximumRepeated = Math.Max(maximumRepeated, currentRepeated);
            }
            else
            {
                currentRepeated = 1;
            }
        }

        return letterCount > 2 && !hasVowel || maximumConsonants >= 4 || maximumRepeated >= 4;
    }

    private static int ToLegacyLower(int codePoint)
    {
        if (codePoint is >= 'A' and <= 'Z')
        {
            return codePoint + 32;
        }

        if (codePoint is >= 0x410 and <= 0x42F)
        {
            return codePoint + 0x20;
        }

        return codePoint == 0x401 ? 0x451 : codePoint;
    }

    private static bool IsLegacyLetter(int codePoint) =>
        codePoint is >= 'a' and <= 'z' or >= 0x430 and <= 0x44F or 0x451;

    private static bool IsLegacyVowel(int codePoint) =>
        codePoint is 'a' or 'e' or 'i' or 'o' or 'u' or 'y' or
            0x430 or 0x435 or 0x451 or 0x438 or 0x43E or 0x443 or 0x44B or 0x44D or 0x44E or 0x44F;
}
