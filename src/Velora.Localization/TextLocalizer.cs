namespace Velora.Localization;

public sealed record LocaleChanged(LocaleCode Previous, LocaleCode Current, bool CatalogAvailable);

/// <summary>
/// A deterministic, filesystem-neutral catalog selector and text translator.
/// Hosts load catalog bytes and explicitly add them to this service.
/// </summary>
public sealed class TextLocalizer : ITextLocalizer
{
    private readonly Dictionary<LocaleCode, LocalizationCatalog> _catalogs = [];
    private LocalizationCatalog? _activeCatalog;

    public TextLocalizer(LocaleCode defaultLocale, IEnumerable<LocalizationCatalog>? catalogs = null)
    {
        if (!defaultLocale.IsValid)
        {
            throw new ArgumentException("A valid default locale is required.", nameof(defaultLocale));
        }

        DefaultLocale = defaultLocale;
        CurrentLocale = defaultLocale;

        if (catalogs is not null)
        {
            foreach (LocalizationCatalog catalog in catalogs)
            {
                AddOrReplace(catalog);
            }
        }

        _catalogs.TryGetValue(CurrentLocale, out _activeCatalog);
    }

    public event EventHandler<LocaleChanged>? LocaleChanged;

    public LocaleCode DefaultLocale { get; }

    public LocaleCode CurrentLocale { get; private set; }

    public IReadOnlyList<LocalizationCatalog> AvailableCatalogs =>
        [.. _catalogs.Values.OrderBy(catalog => catalog.Locale == DefaultLocale ? 0 : 1)
            .ThenBy(catalog => catalog.Locale.Value, StringComparer.Ordinal)];

    public void AddOrReplace(LocalizationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalogs[catalog.Locale] = catalog;
        if (catalog.Locale == CurrentLocale)
        {
            _activeCatalog = catalog;
        }
    }

    /// <summary>
    /// Selects a locale. As in the legacy runtime, an unknown locale remains
    /// selected but has an empty active catalog. The return value reports
    /// whether catalog data was available.
    /// </summary>
    public bool SelectLocale(LocaleCode locale)
    {
        if (!locale.IsValid)
        {
            throw new ArgumentException("A valid locale is required.", nameof(locale));
        }

        if (locale == CurrentLocale)
        {
            return _activeCatalog is not null;
        }

        LocaleCode previous = CurrentLocale;
        CurrentLocale = locale;
        bool available = _catalogs.TryGetValue(locale, out _activeCatalog);
        LocaleChanged?.Invoke(this, new LocaleChanged(previous, locale, available));
        return available;
    }

    public string Translate(string originalText, string context = "", string speaker = "")
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(speaker);

        if (CurrentLocale == DefaultLocale || _activeCatalog is null)
        {
            return originalText;
        }

        string key = LocalizationKey.Generate(originalText, context, speaker);
        return _activeCatalog.TryGetTranslation(key, out string translation)
            && translation.Length > 0
                ? translation
                : originalText;
    }

    public string TranslateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _activeCatalog is not null && _activeCatalog.TryGetTranslation(key, out string translation)
            ? translation
            : key;
    }
}
