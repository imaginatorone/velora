
#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <optional>
#include <functional>
#include <filesystem>

namespace velora::i18n {

struct LanguageInfo {
    std::string code;
    std::string name;
    std::string fontOverride;
};

struct ExtractedString {
    std::string autoKey;
    std::string originalText;
    std::string context;
    std::string speaker;
    std::string sourceFile;
    int sourceLine;
};

class LocalizationManager {
  public:
    explicit LocalizationManager(const std::string& defaultLocale = "en",
                                 std::filesystem::path localesDirectory = "assets/locales");
    ~LocalizationManager();

    [[nodiscard]] std::string translate(const std::string& originalText,
                                        const std::string& context = "",
                                        const std::string& speaker = "") const;

    [[nodiscard]] std::string translateKey(const std::string& key) const;

    void setLocale(const std::string& localeCode);

    [[nodiscard]] const std::string& getLocale() const { return m_currentLocale; }

    [[nodiscard]] const std::vector<LanguageInfo>& getAvailableLanguages() const {
        return m_availableLanguages;
    }

    void detectAvailableLanguages(const std::string& localesDir = "assets/locales");

    bool loadTranslations(const std::string& jsonPath);

    static std::vector<ExtractedString> extractFromScripts(const std::string& scriptsDir);

    static bool generateTemplate(const std::vector<ExtractedString>& strings,
                                 const std::string& outputPath);

    static bool generateKeyMap(const std::vector<ExtractedString>& strings,
                               const std::string& outputPath);

    using LocaleChangedCallback = std::function<void(const std::string& newLocale)>;
    void setOnLocaleChanged(LocaleChangedCallback cb) { m_onLocaleChanged = std::move(cb); }

  private:
    static std::string generateKey(const std::string& text, const std::string& context,
                                   const std::string& speaker);

    std::string m_currentLocale;
    std::string m_defaultLocale;
    std::filesystem::path m_localesDirectory;

    std::unordered_map<std::string, std::string> m_translations;

    std::unordered_map<std::string, std::string> m_textToKey;

    std::vector<LanguageInfo> m_availableLanguages;

    LocaleChangedCallback m_onLocaleChanged;
};

} // namespace velora::i18n
