namespace Velora.Localization;

public interface ITextLocalizer
{
    LocaleCode CurrentLocale { get; }

    string Translate(string originalText, string context = "", string speaker = "");

    string TranslateKey(string key);
}
