using Velora.Localization;

namespace Velora.Localization.Tests;

public sealed class LocalizationKeyTests
{
    [Theory]
    [InlineData("Hello, World!", "say", "mira", "say.mira.hello_world")]
    [InlineData("  MANY   SPACES  ", "menu", "", "menu.many_spaces")]
    [InlineData("Привет 42", "say", "", "say.42")]
    [InlineData("...", "character", "mira", "character.mira.")]
    public void MatchesLegacyAsciiKeyNormalization(
        string text,
        string context,
        string speaker,
        string expected)
    {
        Assert.Equal(expected, LocalizationKey.Generate(text, context, speaker));
    }

    [Fact]
    public void TruncatesNormalizedTextToFiftyCharacters()
    {
        string key = LocalizationKey.Generate(new string('A', 70));

        Assert.Equal(new string('a', 50), key);
    }

    [Fact]
    public void TruncationMatchesLegacyOrderAfterTrailingSpaceNormalization()
    {
        string prefix = new string('a', 49);

        Assert.Equal(prefix, LocalizationKey.Generate(prefix + "   "));
        Assert.Equal(prefix + "_", LocalizationKey.Generate(prefix + " x"));
    }
}
