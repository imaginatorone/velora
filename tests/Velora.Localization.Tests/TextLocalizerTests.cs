using Velora.Localization;

namespace Velora.Localization.Tests;

public sealed class TextLocalizerTests
{
    private static readonly LocaleCode English = LocaleCode.Parse("en");
    private static readonly LocaleCode Russian = LocaleCode.Parse("ru");

    [Fact]
    public void DefaultLocalePreservesOriginalDialogueButStillTranslatesKeys()
    {
        LocalizationCatalog english = Catalog(English, new Dictionary<string, string>
        {
            ["say.mira.hello"] = "localized dialogue",
            ["menu.new_game"] = "New Game",
        });
        TextLocalizer localizer = new(English, [english]);

        Assert.Equal("Hello", localizer.Translate("Hello", "say", "mira"));
        Assert.Equal("New Game", localizer.TranslateKey("menu.new_game"));
    }

    [Fact]
    public void NonDefaultLocaleUsesGeneratedContextAndSpeakerKey()
    {
        LocalizationCatalog russian = Catalog(Russian, new Dictionary<string, string>
        {
            ["say.mira.hello_world"] = "Привет, мир!",
            ["say.mira.empty"] = string.Empty,
        });
        TextLocalizer localizer = new(English, [russian]);

        Assert.True(localizer.SelectLocale(Russian));
        Assert.Equal("Привет, мир!", localizer.Translate("Hello, World!", "say", "mira"));
        Assert.Equal("Empty", localizer.Translate("Empty", "say", "mira"));
        Assert.Equal("Missing", localizer.Translate("Missing", "say", "mira"));
    }

    [Fact]
    public void UnknownLocaleRemainsSelectedWithNoCatalogLikeLegacyRuntime()
    {
        TextLocalizer localizer = new(English, [Catalog(English, new Dictionary<string, string>())]);
        LocaleChanged? change = null;
        localizer.LocaleChanged += (_, value) => change = value;

        LocaleCode unknown = LocaleCode.Parse("zz-ZZ");
        Assert.False(localizer.SelectLocale(unknown));

        Assert.Equal(unknown, localizer.CurrentLocale);
        Assert.Equal("Hello", localizer.Translate("Hello", "say", "mira"));
        Assert.Equal("menu.new_game", localizer.TranslateKey("menu.new_game"));
        Assert.Equal(new LocaleChanged(English, unknown, CatalogAvailable: false), change);
    }

    [Fact]
    public void AvailableCatalogsPlaceDefaultFirstThenSortOrdinally()
    {
        TextLocalizer localizer = new(Russian,
        [
            Catalog(LocaleCode.Parse("zh"), new Dictionary<string, string>()),
            Catalog(English, new Dictionary<string, string>()),
            Catalog(Russian, new Dictionary<string, string>()),
        ]);

        Assert.Equal(["ru", "en", "zh"], localizer.AvailableCatalogs.Select(catalog => catalog.Locale.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../ru")]
    [InlineData("ru_RU")]
    [InlineData("-ru")]
    [InlineData("ru-")]
    public void RejectsUnsafeLocaleCodes(string value)
    {
        Assert.False(LocaleCode.TryParse(value, out _));
    }

    private static LocalizationCatalog Catalog(
        LocaleCode locale,
        IReadOnlyDictionary<string, string> translations) =>
        new(locale, locale.Value, translations);
}
