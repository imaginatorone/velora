
#include "LocalizationManager.h"
#include <nlohmann/json.hpp>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <algorithm>
#include <regex>
#include <cctype>

namespace fs = std::filesystem;
using json = nlohmann::json;

namespace velora::i18n {

LocalizationManager::LocalizationManager(const std::string& defaultLocale,
                                         std::filesystem::path localesDirectory)
    : m_currentLocale(defaultLocale), m_defaultLocale(defaultLocale),
      m_localesDirectory(std::move(localesDirectory)) {}

LocalizationManager::~LocalizationManager() = default;

std::string LocalizationManager::generateKey(const std::string& text, const std::string& context,
                                             const std::string& speaker) {
    std::string normalized;
    for (char c : text) {
        const auto byte = static_cast<unsigned char>(c);
        if (std::isalnum(byte))
            normalized += static_cast<char>(std::tolower(byte));
        else if (c == ' ' && !normalized.empty() && normalized.back() != '_')
            normalized += '_';
    }
    while (!normalized.empty() && normalized.back() == '_')
        normalized.pop_back();
    if (normalized.size() > 50)
        normalized = normalized.substr(0, 50);

    std::string key;
    if (!context.empty())
        key = context + ".";
    if (!speaker.empty())
        key += speaker + ".";
    key += normalized;
    return key;
}

std::string LocalizationManager::translate(const std::string& originalText,
                                           const std::string& context,
                                           const std::string& speaker) const {
    if (m_currentLocale == m_defaultLocale)
        return originalText;

    auto textIt = m_textToKey.find(originalText);
    if (textIt != m_textToKey.end()) {
        auto transIt = m_translations.find(textIt->second);
        if (transIt != m_translations.end() && !transIt->second.empty()) {
            return transIt->second;
        }
    }

    std::string key = generateKey(originalText, context, speaker);
    auto transIt = m_translations.find(key);
    if (transIt != m_translations.end() && !transIt->second.empty()) {
        return transIt->second;
    }

    return originalText;
}

std::string LocalizationManager::translateKey(const std::string& key) const {
    auto it = m_translations.find(key);
    if (it != m_translations.end())
        return it->second;
    return key;
}

void LocalizationManager::setLocale(const std::string& localeCode) {
    if (localeCode == m_currentLocale)
        return;

    std::string oldLocale = m_currentLocale;
    m_currentLocale = localeCode;

    const fs::path path = m_localesDirectory / (localeCode + ".json");
    if (fs::exists(path)) {
        loadTranslations(path.string());
        std::cout << "[i18n] Locale changed: " << oldLocale << " -> " << localeCode << "\n";
    } else {
        std::cerr << "[i18n] Locale file not found: " << path << "\n";
        m_translations.clear();
    }

    if (m_onLocaleChanged)
        m_onLocaleChanged(localeCode);
}

void LocalizationManager::detectAvailableLanguages(const std::string& localesDir) {
    m_availableLanguages.clear();

    if (!fs::exists(localesDir)) {
        m_availableLanguages.push_back({"en", "English", ""});
        return;
    }

    for (const auto& entry : fs::directory_iterator(localesDir)) {
        if (entry.path().extension() == ".json" && entry.path().stem() != "template" &&
            entry.path().stem() != "key_map") {
            std::string code = entry.path().stem().string();

            std::string name = code;
            try {
                std::ifstream file(entry.path());
                json j = json::parse(file);
                if (j.contains("__meta") && j["__meta"].contains("language_name")) {
                    name = j["__meta"]["language_name"].get<std::string>();
                }
            } catch (...) {
            }

            static const std::unordered_map<std::string, std::string> knownNames = {
                {"en", "English"},    {"ru", "Russian"}, {"ja", "Japanese"}, {"zh", "Chinese"},
                {"ko", "Korean"},     {"de", "German"},  {"fr", "French"},   {"es", "Spanish"},
                {"pt", "Portuguese"}, {"it", "Italian"}, {"pl", "Polish"},   {"uk", "Ukrainian"},
            };
            auto knownIt = knownNames.find(code);
            if (knownIt != knownNames.end() && name == code)
                name = knownIt->second;

            m_availableLanguages.push_back({code, name, ""});
        }
    }

    std::sort(m_availableLanguages.begin(), m_availableLanguages.end(),
              [this](const LanguageInfo& a, const LanguageInfo& b) {
                  if (a.code == m_defaultLocale)
                      return true;
                  if (b.code == m_defaultLocale)
                      return false;
                  return a.code < b.code;
              });

    std::cout << "[i18n] Detected " << m_availableLanguages.size() << " languages.\n";
}

bool LocalizationManager::loadTranslations(const std::string& jsonPath) {
    try {
        std::ifstream file(jsonPath);
        json j = json::parse(file);

        m_translations.clear();
        m_textToKey.clear();

        for (auto& [key, value] : j.items()) {
            if (key.starts_with("__"))
                continue;
            if (value.is_string()) {
                m_translations[key] = value.get<std::string>();
            }
        }

        std::cout << "[i18n] Loaded " << m_translations.size() << " translations from " << jsonPath
                  << "\n";
        return true;
    } catch (const std::exception& e) {
        std::cerr << "[i18n] Failed to load: " << jsonPath << " - " << e.what() << "\n";
        return false;
    }
}

std::vector<ExtractedString>
LocalizationManager::extractFromScripts(const std::string& scriptsDir) {

    std::vector<ExtractedString> strings;
    std::cout << "[i18n] Scanning scripts in: " << scriptsDir << "\n";

    if (!fs::exists(scriptsDir)) {
        std::cerr << "[i18n] Scripts directory not found: " << scriptsDir << "\n";
        return strings;
    }

    for (const auto& entry : fs::recursive_directory_iterator(scriptsDir)) {
        if (entry.path().extension() != ".vel")
            continue;

        std::ifstream file(entry.path());
        std::string line;
        int lineNum = 0;
        std::string filePath = entry.path().string();

        while (std::getline(file, line)) {
            lineNum++;

            std::string trimmed = line;
            while (!trimmed.empty() && (trimmed[0] == ' ' || trimmed[0] == '\t'))
                trimmed = trimmed.substr(1);
            if (trimmed.empty() || trimmed.starts_with("--"))
                continue;

            std::regex charRegex(R"vl(character\s+(\w+)\s*=\s*"([^"]+)")vl");
            std::smatch charMatch;
            if (std::regex_search(line, charMatch, charRegex)) {
                ExtractedString es;
                es.context = "character";
                es.speaker = charMatch[1].str();
                es.originalText = charMatch[2].str();
                es.autoKey = generateKey(es.originalText, es.context, es.speaker);
                es.sourceFile = filePath;
                es.sourceLine = lineNum;
                strings.push_back(es);
                continue;
            }

            std::regex sayRegex(R"vl((\w+)\s+"([^"]+)")vl");
            std::smatch sayMatch;
            if (std::regex_search(line, sayMatch, sayRegex)) {
                std::string potentialCmd = sayMatch[1].str();
                static const std::vector<std::string> nonDialogue = {
                    "scene",  "show", "play", "stop",   "hide",      "character",
                    "define", "with", "set",  "config", "transition"};
                bool isCmd = std::find(nonDialogue.begin(), nonDialogue.end(), potentialCmd) !=
                             nonDialogue.end();
                if (!isCmd) {
                    ExtractedString es;
                    es.context = "say";
                    es.speaker = potentialCmd;
                    es.originalText = sayMatch[2].str();
                    es.autoKey = generateKey(es.originalText, es.context, es.speaker);
                    es.sourceFile = filePath;
                    es.sourceLine = lineNum;
                    strings.push_back(es);
                }
            }

            std::regex menuRegex(R"vl(^\s*"([^"]+)"\s*:)vl");
            std::smatch menuMatch;
            if (std::regex_search(line, menuMatch, menuRegex)) {
                ExtractedString es;
                es.context = "menu";
                es.originalText = menuMatch[1].str();
                es.autoKey = generateKey(es.originalText, es.context, "");
                es.sourceFile = filePath;
                es.sourceLine = lineNum;
                strings.push_back(es);
            }
        }
    }

    std::cout << "[i18n] Extracted " << strings.size() << " translatable strings.\n";
    return strings;
}

bool LocalizationManager::generateTemplate(const std::vector<ExtractedString>& strings,
                                           const std::string& outputPath) {
    json j;
    j["__meta"] = {{"generated_by", "Velora Engine Auto-Extractor"},
                   {"language_name", "FILL_IN_LANGUAGE_NAME"},
                   {"language_code", "FILL_IN_CODE"}};

    for (const auto& s : strings) {
        j[s.autoKey] = "";
    }

    json ref;
    ref["__meta"] = {{"description", "Reference: original text for each key"}};
    for (const auto& s : strings) {
        ref[s.autoKey] = s.originalText;
    }

    fs::create_directories(fs::path(outputPath).parent_path());
    std::ofstream file(outputPath);
    if (!file) {
        std::cerr << "[i18n] Cannot write template: " << outputPath << "\n";
        return false;
    }
    file << j.dump(4) << "\n";

    std::string refPath = fs::path(outputPath).parent_path().string() + "/reference.json";
    std::ofstream refFile(refPath);
    if (refFile)
        refFile << ref.dump(4) << "\n";

    std::cout << "[i18n] Template generated: " << outputPath << "\n";
    std::cout << "[i18n] Reference generated: " << refPath << "\n";
    return true;
}

bool LocalizationManager::generateKeyMap(const std::vector<ExtractedString>& strings,
                                         const std::string& outputPath) {
    json j;
    for (const auto& s : strings) {
        j[s.originalText] = {{"key", s.autoKey},
                             {"context", s.context},
                             {"speaker", s.speaker},
                             {"file", s.sourceFile},
                             {"line", s.sourceLine}};
    }

    std::ofstream file(outputPath);
    if (!file)
        return false;
    file << j.dump(4) << "\n";

    std::cout << "[i18n] Key map generated: " << outputPath << "\n";
    return true;
}

} // namespace velora::i18n
