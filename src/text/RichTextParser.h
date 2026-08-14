
#pragma once

#include <SDL3/SDL.h>
#include <SDL3_ttf/SDL_ttf.h>
#include <string>
#include <vector>
#include <functional>
#include <filesystem>

namespace velora::text {

struct InlineTag {
    size_t charIndex;
    std::string command;
    std::string argument;
    bool fired = false;
};

struct TextSegment {
    std::string text;
    SDL_Color color = {255, 255, 255, 255};
    bool bold = false;
    bool italic = false;
};

class RichTextParser {
  public:
    explicit RichTextParser(std::filesystem::path assetRoot = {});
    ~RichTextParser();

    void beginDialogue(const std::string& speaker, const std::string& speakerColor,
                       const std::string& rawText);

    void update(double dt);

    void render(SDL_Renderer* renderer);

    void skipTypewriter();

    [[nodiscard]] bool isComplete() const { return m_complete; }

    [[nodiscard]] bool isActive() const { return m_active; }

    void showMenu(const std::vector<std::string>& choices);
    void hideMenu() {
        m_menuChoices.clear();
        m_menuActive = false;
        m_menuHover = -1;
    }
    void renderMenu(SDL_Renderer* renderer);
    int handleMenuClick(float mouseX, float mouseY);
    void updateMenuHover(float mouseX, float mouseY);
    [[nodiscard]] bool isMenuActive() const { return m_menuActive; }

    void setCharactersPerSecond(float cps) { m_charsPerSecond = cps; }
    void setDialogueBoxOpacity(float alpha) { m_boxOpacity = alpha; }
    [[nodiscard]] TTF_Font* getFont() const { return m_font; }
    [[nodiscard]] TTF_Font* getFontBold() const { return m_fontBold; }

    void setAccentColor(uint8_t r, uint8_t g, uint8_t b) { m_accentColor = {r, g, b, 255}; }
    void setAccentColor(SDL_Color c) { m_accentColor = c; }
    [[nodiscard]] SDL_Color getAccentColor() const { return m_accentColor; }

    static SDL_Color hexToColor(const std::string& hex);

    using TagCallback = std::function<void(const std::string& command, const std::string& arg)>;
    void setTagCallback(TagCallback cb) { m_tagCallback = std::move(cb); }

    using VariableResolver = std::function<std::string(const std::string& varName)>;
    void setVariableResolver(VariableResolver resolver) { m_varResolver = std::move(resolver); }

  private:
    void parseText(const std::string& rawText);

    std::string interpolateVariables(const std::string& text);

    bool m_active = false;
    bool m_complete = false;

    std::string m_speaker;
    SDL_Color m_speakerColor = {255, 255, 255, 255};

    std::string m_plainText;
    std::vector<TextSegment> m_segments;
    std::vector<InlineTag> m_tags;

    float m_charsPerSecond = 30.0f;
    float m_currentSpeed = 30.0f;
    float m_charAccumulator = 0.0f;
    size_t m_visibleChars = 0;
    float m_waitTimer = 0.0f;
    size_t m_totalCodePoints = 0;

    float m_boxOpacity = 0.75f;
    SDL_Color m_accentColor = {172, 204, 157, 255};

    TagCallback m_tagCallback;

    VariableResolver m_varResolver;

    TTF_Font* m_font = nullptr;
    TTF_Font* m_fontBold = nullptr;
    float m_fontSize = 24.0f;
    bool m_fontLoaded = false;
    std::filesystem::path m_assetRoot;
    void loadFont();
    SDL_Texture* renderTextLine(SDL_Renderer* renderer, const std::string& text, SDL_Color color,
                                TTF_Font* font, float maxW);

    bool m_menuActive = false;
    std::vector<std::string> m_menuChoices;
    std::vector<SDL_FRect> m_menuRects;
    int m_menuHover = -1;
};

} // namespace velora::text
