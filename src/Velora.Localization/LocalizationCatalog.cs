using System.Collections.ObjectModel;

namespace Velora.Localization;

public sealed class LocalizationCatalog
{
    private readonly ReadOnlyDictionary<string, string> _translations;

    public LocalizationCatalog(
        LocaleCode locale,
        string languageName,
        IReadOnlyDictionary<string, string> translations,
        string fontOverride = "")
    {
        ArgumentNullException.ThrowIfNull(languageName);
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(fontOverride);
        if (!locale.IsValid)
        {
            throw new ArgumentException("A catalog requires a valid locale code.", nameof(locale));
        }

        Locale = locale;
        LanguageName = languageName;
        FontOverride = fontOverride;
        _translations = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(translations, StringComparer.Ordinal));
    }

    public LocaleCode Locale { get; }

    public string LanguageName { get; }

    public string FontOverride { get; }

    public IReadOnlyDictionary<string, string> Translations => _translations;

    public bool TryGetTranslation(string key, out string translation)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _translations.TryGetValue(key, out translation!);
    }
}
