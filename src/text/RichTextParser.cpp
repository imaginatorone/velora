
#include "RichTextParser.h"

#include <SDL3/SDL.h>
#include <SDL3_ttf/SDL_ttf.h>

#include <iostream>
#include <sstream>
#include <algorithm>
#include <cmath>
#include <filesystem>

namespace velora::text {

// Typewriter progress is measured in code points, not UTF-8 bytes.
static size_t utf8Length(const std::string& s) {
    size_t count = 0;
    for (size_t i = 0; i < s.size();) {
        unsigned char c = static_cast<unsigned char>(s[i]);
        if (c < 0x80)
            i += 1;
        else if (c < 0xE0)
            i += 2;
        else if (c < 0xF0)
            i += 3;
        else
            i += 4;
        count++;
    }
    return count;
}

static size_t utf8ByteOffset(const std::string& s, size_t charCount) {
    size_t bytePos = 0;
    size_t chars = 0;
    while (bytePos < s.size() && chars < charCount) {
        unsigned char c = static_cast<unsigned char>(s[bytePos]);
        if (c < 0x80)
            bytePos += 1;
        else if (c < 0xE0)
            bytePos += 2;
        else if (c < 0xF0)
            bytePos += 3;
        else
            bytePos += 4;
        chars++;
    }
    return bytePos;
}

RichTextParser::RichTextParser(std::filesystem::path assetRoot)
    : m_assetRoot(std::move(assetRoot)) {
    loadFont();
}

RichTextParser::~RichTextParser() {
    if (m_fontBold && m_fontBold != m_font)
        TTF_CloseFont(m_fontBold);
    if (m_font)
        TTF_CloseFont(m_font);
}

void RichTextParser::loadFont() {
    std::vector<std::filesystem::path> fontPaths = {
        m_assetRoot / "fonts/default.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/TTF/DejaVuSans.ttf",
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/arial.ttf",
    };
    std::vector<std::filesystem::path> boldPaths = {
        m_assetRoot / "fonts/default_bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "C:/Windows/Fonts/segoeuib.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
    };

    for (const auto& path : fontPaths) {
        if (std::filesystem::exists(path)) {
            m_font = TTF_OpenFont(path.string().c_str(), m_fontSize);
            if (m_font) {
                std::cout << "[RichText] Font loaded: " << path << "\n";
                break;
            }
        }
    }

    for (const auto& path : boldPaths) {
        if (std::filesystem::exists(path)) {
            m_fontBold = TTF_OpenFont(path.string().c_str(), m_fontSize);
            if (m_fontBold)
                break;
        }
    }

    if (!m_fontBold && m_font)
        m_fontBold = m_font;

    m_fontLoaded = (m_font != nullptr);
    if (!m_fontLoaded) {
        std::cerr << "[RichText] WARNING: No font found! Text will not be displayed.\n";
    }
}

void RichTextParser::beginDialogue(const std::string& speaker, const std::string& speakerColor,
                                   const std::string& rawText) {
    m_speaker = speaker;
    m_speakerColor = hexToColor(speakerColor);
    m_active = true;
    m_complete = false;
    m_visibleChars = 0;
    m_charAccumulator = 0.0f;
    m_waitTimer = 0.0f;
    m_currentSpeed = m_charsPerSecond;
    m_plainText.clear();
    m_segments.clear();
    m_tags.clear();

    std::string resolved = interpolateVariables(rawText);
    parseText(resolved);
    m_totalCodePoints = utf8Length(m_plainText);
}

void RichTextParser::parseText(const std::string& rawText) {
    std::string plain;
    SDL_Color currentColor = {255, 255, 255, 255};
    bool bold = false;
    size_t i = 0;

    TextSegment currentSeg;
    currentSeg.color = currentColor;

    while (i < rawText.size()) {
        if (rawText[i] == '{') {
            size_t end = rawText.find('}', i);
            if (end == std::string::npos) {
                plain += rawText[i];
                i++;
                continue;
            }

            std::string tagContent = rawText.substr(i + 1, end - i - 1);
            i = end + 1;

            size_t colonPos = tagContent.find(':');
            std::string cmd, arg;
            if (colonPos != std::string::npos) {
                cmd = tagContent.substr(0, colonPos);
                arg = tagContent.substr(colonPos + 1);
            } else {
                cmd = tagContent;
            }

            if (cmd == "color") {
                if (!currentSeg.text.empty())
                    m_segments.push_back(currentSeg);
                currentSeg = TextSegment{};
                currentSeg.color = hexToColor(arg);
                currentSeg.bold = bold;
            } else if (cmd == "/color") {
                if (!currentSeg.text.empty())
                    m_segments.push_back(currentSeg);
                currentSeg = TextSegment{};
                currentSeg.color = {255, 255, 255, 255};
                currentSeg.bold = bold;
            } else if (cmd == "bold") {
                if (!currentSeg.text.empty())
                    m_segments.push_back(currentSeg);
                currentSeg = TextSegment{};
                currentSeg.color = currentColor;
                bold = true;
                currentSeg.bold = true;
            } else if (cmd == "/bold") {
                if (!currentSeg.text.empty())
                    m_segments.push_back(currentSeg);
                currentSeg = TextSegment{};
                currentSeg.color = currentColor;
                bold = false;
                currentSeg.bold = false;
            } else {
                InlineTag tag;
                tag.charIndex = utf8Length(plain);
                tag.command = cmd;
                tag.argument = arg;
                m_tags.push_back(tag);
            }
        } else {
            plain += rawText[i];
            currentSeg.text += rawText[i];
            i++;
        }
    }

    if (!currentSeg.text.empty())
        m_segments.push_back(currentSeg);
    m_plainText = plain;

    std::sort(m_tags.begin(), m_tags.end(),
              [](const InlineTag& a, const InlineTag& b) { return a.charIndex < b.charIndex; });
}

void RichTextParser::update(double dt) {
    if (!m_active || m_complete)
        return;

    if (m_waitTimer > 0.0f) {
        m_waitTimer -= static_cast<float>(dt);
        return;
    }

    m_charAccumulator += m_currentSpeed * static_cast<float>(dt);

    while (m_charAccumulator >= 1.0f && m_visibleChars < m_totalCodePoints) {
        m_charAccumulator -= 1.0f;
        m_visibleChars++;

        for (auto& tag : m_tags) {
            if (tag.fired)
                continue;
            if (tag.charIndex <= m_visibleChars) {
                tag.fired = true;
                if (tag.command == "wait") {
                    try {
                        m_waitTimer = std::max(0.0f, std::stof(tag.argument));
                    } catch (...) {
                        m_waitTimer = 0.0f;
                    }
                } else if (tag.command == "speed") {
                    try {
                        m_currentSpeed = std::clamp(std::stof(tag.argument), 1.0f, 240.0f);
                    } catch (...) {
                        m_currentSpeed = m_charsPerSecond;
                    }
                } else {
                    if (m_tagCallback)
                        m_tagCallback(tag.command, tag.argument);
                }
            }
        }
    }

    if (m_visibleChars >= m_totalCodePoints) {
        m_complete = true;
    }
}

void RichTextParser::skipTypewriter() {
    m_visibleChars = m_totalCodePoints;
    m_complete = true;
    for (auto& tag : m_tags) {
        if (!tag.fired) {
            tag.fired = true;
            if (tag.command != "wait" && tag.command != "speed" && m_tagCallback) {
                m_tagCallback(tag.command, tag.argument);
            }
        }
    }
}

SDL_Texture* RichTextParser::renderTextLine(SDL_Renderer* renderer, const std::string& text,
                                            SDL_Color color, TTF_Font* font, float maxW) {
    if (!font || text.empty())
        return nullptr;

    SDL_Surface* surface = TTF_RenderText_Blended_Wrapped(font, text.c_str(), text.size(), color,
                                                          static_cast<int>(maxW));
    if (!surface)
        return nullptr;

    SDL_Texture* tex = SDL_CreateTextureFromSurface(renderer, surface);
    SDL_DestroySurface(surface);
    return tex;
}

void RichTextParser::render(SDL_Renderer* renderer) {
    if (!m_active)
        return;

    int logW = 0, logH = 0;
    SDL_GetRenderLogicalPresentation(renderer, &logW, &logH, nullptr);
    if (logW <= 0)
        logW = 1280;
    if (logH <= 0)
        logH = 720;
    float screenW = static_cast<float>(logW);
    float screenH = static_cast<float>(logH);

    SDL_SetRenderDrawBlendMode(renderer, SDL_BLENDMODE_BLEND);
    uint8_t boxAlpha = static_cast<uint8_t>(m_boxOpacity * 255.0f);

    float boxW = screenW * 0.88f;
    float boxH = screenH * 0.25f;
    float boxX = (screenW - boxW) * 0.5f;
    float boxY = screenH - boxH - 20.0f;
    float padding = 20.0f;

    SDL_SetRenderDrawColor(renderer, 10, 10, 20, boxAlpha);
    SDL_FRect boxRect = {boxX, boxY, boxW, boxH};
    SDL_RenderFillRect(renderer, &boxRect);

    SDL_SetRenderDrawColor(renderer, m_accentColor.r, m_accentColor.g, m_accentColor.b,
                           static_cast<uint8_t>(boxAlpha * 0.6f));
    SDL_FRect topLine = {boxX, boxY, boxW, 2.0f};
    SDL_RenderFillRect(renderer, &topLine);

    SDL_SetRenderDrawColor(renderer, 60, 60, 90, static_cast<uint8_t>(boxAlpha * 0.4f));
    SDL_FRect borderRect = {boxX - 1, boxY - 1, boxW + 2, boxH + 2};
    SDL_RenderRect(renderer, &borderRect);

    if (!m_speaker.empty()) {
        float nameTextW = 120.0f;
        float nameH = 32.0f;
        TTF_Font* nameFont = m_fontBold ? m_fontBold : m_font;
        if (m_fontLoaded && nameFont) {
            int tw = 0, th = 0;
            TTF_GetStringSize(nameFont, m_speaker.c_str(), m_speaker.size(), &tw, &th);
            nameTextW = static_cast<float>(tw);
        }
        float namePad = 20.0f;
        float nameW = std::max(120.0f, nameTextW + namePad);
        float nameX = boxX + 15.0f;
        float nameY = boxY - nameH - 4.0f;

        SDL_SetRenderDrawColor(renderer, m_speakerColor.r / 3, m_speakerColor.g / 3,
                               m_speakerColor.b / 3, boxAlpha);
        SDL_FRect nameRect = {nameX, nameY, nameW, nameH};
        SDL_RenderFillRect(renderer, &nameRect);

        SDL_SetRenderDrawColor(renderer, m_speakerColor.r, m_speakerColor.g, m_speakerColor.b,
                               boxAlpha);
        SDL_FRect nameAccent = {nameX, nameY + nameH - 2, nameW, 2.0f};
        SDL_RenderFillRect(renderer, &nameAccent);

        if (m_fontLoaded) {
            SDL_Rect clipRect = {static_cast<int>(nameX), static_cast<int>(nameY),
                                 static_cast<int>(nameW), static_cast<int>(nameH)};
            SDL_SetRenderClipRect(renderer, &clipRect);

            SDL_Texture* nameTex =
                renderTextLine(renderer, m_speaker, m_speakerColor, nameFont, nameW - 10.0f);
            if (nameTex) {
                float tw = 0, th = 0;
                SDL_GetTextureSize(nameTex, &tw, &th);
                SDL_FRect dst = {nameX + 8.0f, nameY + (nameH - th) * 0.5f, tw, th};
                SDL_RenderTexture(renderer, nameTex, nullptr, &dst);
                SDL_DestroyTexture(nameTex);
            }

            SDL_SetRenderClipRect(renderer, nullptr);
        }
    }

    if (m_fontLoaded && !m_plainText.empty() && m_visibleChars > 0) {
        SDL_Rect textClip = {static_cast<int>(boxX), static_cast<int>(boxY), static_cast<int>(boxW),
                             static_cast<int>(boxH)};
        SDL_SetRenderClipRect(renderer, &textClip);

        size_t byteLen = utf8ByteOffset(m_plainText, m_visibleChars);
        std::string visibleText = m_plainText.substr(0, byteLen);

        float textX = boxX + padding + 5.0f;
        float textY = boxY + padding;
        float textMaxW = boxW - padding * 2.0f - 10.0f;

        SDL_Color textColor = {230, 230, 240, 255};
        SDL_Texture* textTex = renderTextLine(renderer, visibleText, textColor, m_font, textMaxW);
        if (textTex) {
            float tw = 0, th = 0;
            SDL_GetTextureSize(textTex, &tw, &th);
            SDL_FRect dst = {textX, textY, tw, th};
            SDL_RenderTexture(renderer, textTex, nullptr, &dst);
            SDL_DestroyTexture(textTex);
        }

        SDL_SetRenderClipRect(renderer, nullptr);

        if (!m_complete) {
            float blinkPhase = SDL_GetTicks() % 1000 < 500 ? 1.0f : 0.0f;
            if (blinkPhase > 0.5f) {
                SDL_SetRenderDrawColor(renderer, 200, 200, 255, 180);
                float cursorX = textX + 2.0f;
                float cursorY = textY + 40.0f;
                SDL_FRect cursor = {cursorX, cursorY, 12.0f, 2.0f};
                SDL_RenderFillRect(renderer, &cursor);
            }
        }
    }

    if (m_complete) {
        float indicatorPhase = (SDL_GetTicks() % 1200) / 1200.0f;
        float bounce = std::sin(indicatorPhase * 3.14159f * 2.0f) * 3.0f;

        float triX = boxX + boxW - 35.0f;
        float triY = boxY + boxH - 20.0f + bounce;

        SDL_SetRenderDrawColor(renderer, m_accentColor.r, m_accentColor.g, m_accentColor.b, 200);
        for (int row = 0; row < 8; row++) {
            float halfW = 8.0f - row;
            SDL_FRect line = {triX - halfW, triY + row, halfW * 2.0f, 1.0f};
            SDL_RenderFillRect(renderer, &line);
        }
    }
}

SDL_Color RichTextParser::hexToColor(const std::string& hex) {
    SDL_Color color = {255, 255, 255, 255};
    std::string h = hex;
    if (h.empty())
        return color;
    if (h[0] == '#')
        h = h.substr(1);

    if (h.size() >= 6) {
        color.r = static_cast<uint8_t>(std::stoul(h.substr(0, 2), nullptr, 16));
        color.g = static_cast<uint8_t>(std::stoul(h.substr(2, 2), nullptr, 16));
        color.b = static_cast<uint8_t>(std::stoul(h.substr(4, 2), nullptr, 16));
    }
    if (h.size() >= 8) {
        color.a = static_cast<uint8_t>(std::stoul(h.substr(6, 2), nullptr, 16));
    }
    return color;
}

std::string RichTextParser::interpolateVariables(const std::string& text) {
    if (!m_varResolver)
        return text;

    std::string result;
    result.reserve(text.size());
    size_t i = 0;

    while (i < text.size()) {
        if (text[i] == '[') {
            size_t end = text.find(']', i);
            if (end != std::string::npos) {
                std::string varName = text.substr(i + 1, end - i - 1);
                if (!varName.empty()) {
                    result += m_varResolver(varName);
                } else {
                    result += "[]";
                }
                i = end + 1;
            } else {
                result += text[i];
                i++;
            }
        } else {
            result += text[i];
            i++;
        }
    }
    return result;
}

void RichTextParser::showMenu(const std::vector<std::string>& choices) {
    m_menuChoices = choices;
    m_menuRects.clear();
    m_menuActive = true;
    m_menuHover = -1;
    m_active = false;
}

void RichTextParser::renderMenu(SDL_Renderer* renderer) {
    if (!m_menuActive || m_menuChoices.empty())
        return;

    int winW = 1280, winH = 720;
    SDL_GetRenderLogicalPresentation(renderer, &winW, &winH, nullptr);
    float screenW = static_cast<float>(winW);
    float screenH = static_cast<float>(winH);

    float choiceW = screenW * 0.5f;
    float choiceH = 50.0f;
    float gap = 8.0f;
    int count = static_cast<int>(m_menuChoices.size());
    float totalH = count * choiceH + (count - 1) * gap;
    float startX = (screenW - choiceW) * 0.5f;
    float startY = (screenH - totalH) * 0.5f;

    m_menuRects.resize(count);

    SDL_SetRenderDrawBlendMode(renderer, SDL_BLENDMODE_BLEND);

    for (int i = 0; i < count; i++) {
        float y = startY + i * (choiceH + gap);
        SDL_FRect rect = {startX, y, choiceW, choiceH};
        m_menuRects[i] = rect;

        bool hovered = (i == m_menuHover);

        if (hovered) {
            SDL_SetRenderDrawColor(renderer, m_accentColor.r, m_accentColor.g, m_accentColor.b,
                                   220);
        } else {
            SDL_SetRenderDrawColor(renderer, 20, 20, 35, 200);
        }
        SDL_RenderFillRect(renderer, &rect);

        if (hovered) {
            SDL_SetRenderDrawColor(renderer,
                                   static_cast<uint8_t>(std::min(255, m_accentColor.r + 50)),
                                   static_cast<uint8_t>(std::min(255, m_accentColor.g + 50)),
                                   static_cast<uint8_t>(std::min(255, m_accentColor.b + 50)), 255);
        } else {
            SDL_SetRenderDrawColor(renderer, 80, 80, 120, 180);
        }
        SDL_RenderRect(renderer, &rect);

        SDL_SetRenderDrawColor(renderer, m_accentColor.r, m_accentColor.g, m_accentColor.b,
                               hovered ? 255 : 120);
        SDL_FRect accent = {startX, y, 4.0f, choiceH};
        SDL_RenderFillRect(renderer, &accent);

        if (m_fontLoaded) {
            SDL_Color textCol =
                hovered ? SDL_Color{255, 255, 255, 255} : SDL_Color{200, 200, 220, 255};
            SDL_Texture* tex =
                renderTextLine(renderer, m_menuChoices[i], textCol, m_font, choiceW - 40.0f);
            if (tex) {
                float tw = 0, th = 0;
                SDL_GetTextureSize(tex, &tw, &th);
                SDL_FRect dst = {startX + 20.0f, y + (choiceH - th) * 0.5f, tw, th};
                SDL_RenderTexture(renderer, tex, nullptr, &dst);
                SDL_DestroyTexture(tex);
            }
        }

        if (m_fontLoaded) {
            std::string num = std::to_string(i + 1) + ".";
            SDL_Color numCol = m_accentColor;
            SDL_Texture* numTex =
                renderTextLine(renderer, num, numCol, m_fontBold ? m_fontBold : m_font, 30.0f);
            if (numTex) {
                float tw = 0, th = 0;
                SDL_GetTextureSize(numTex, &tw, &th);
                SDL_FRect dst = {startX - tw - 10.0f, y + (choiceH - th) * 0.5f, tw, th};
                SDL_RenderTexture(renderer, numTex, nullptr, &dst);
                SDL_DestroyTexture(numTex);
            }
        }
    }
}

int RichTextParser::handleMenuClick(float mouseX, float mouseY) {
    if (!m_menuActive)
        return -1;
    for (int i = 0; i < static_cast<int>(m_menuRects.size()); i++) {
        const auto& r = m_menuRects[i];
        if (mouseX >= r.x && mouseX <= r.x + r.w && mouseY >= r.y && mouseY <= r.y + r.h) {
            return i;
        }
    }
    return -1;
}

void RichTextParser::updateMenuHover(float mouseX, float mouseY) {
    if (!m_menuActive)
        return;
    m_menuHover = -1;
    for (int i = 0; i < static_cast<int>(m_menuRects.size()); i++) {
        const auto& r = m_menuRects[i];
        if (mouseX >= r.x && mouseX <= r.x + r.w && mouseY >= r.y && mouseY <= r.y + r.h) {
            m_menuHover = i;
            break;
        }
    }
}

} // namespace velora::text
