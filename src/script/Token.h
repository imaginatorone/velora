
#pragma once

#include <string>
#include <string_view>
#include <unordered_map>
#include <format>

namespace velora::script {

enum class TokenType {
    STRING_LITERAL,
    NUMBER_LITERAL,
    IDENTIFIER,
    COLOR_LITERAL,

    KW_LABEL,
    KW_JUMP,
    KW_END,

    KW_SCENE,
    KW_SHOW,
    KW_HIDE,
    KW_WITH,
    KW_AT,
    KW_WAIT,

    KW_PLAY,
    KW_STOP,
    KW_BGM,
    KW_SFX,
    KW_VOICE,
    KW_MENU,

    KW_CHARACTER,
    KW_SET,
    KW_IF,
    KW_ELIF,
    KW_ELSE,
    KW_AND,
    KW_OR,
    KW_NOT,
    KW_TRUE,
    KW_FALSE,

    KW_CONFIG,

    KW_TWEEN,
    KW_EMOTION,

    KW_INPUT,
    KW_META,
    KW_PARTICLE,

    KW_SCREEN,
    KW_PANEL,
    KW_TEXT,
    KW_IMAGEBUTTON,

    OP_ASSIGN,
    OP_EQUALS,
    OP_NOT_EQUALS,
    OP_LESS,
    OP_GREATER,
    OP_LESS_EQ,
    OP_GREATER_EQ,
    OP_PLUS,
    OP_MINUS,
    OP_MULTIPLY,
    OP_DIVIDE,
    OP_DOT,
    OP_ARROW,

    COLON,
    COMMA,
    LPAREN,
    RPAREN,
    NEWLINE,
    EOF_TOKEN,
};

struct Token {
    TokenType type;
    std::string value;
    int line;
    int column;
    std::string filename;

    Token() : type(TokenType::EOF_TOKEN), line(0), column(0) {}

    Token(TokenType t, std::string val, int ln, int col, std::string file = "")
        : type(t), value(std::move(val)), line(ln), column(col), filename(std::move(file)) {}

    [[nodiscard]] std::string location() const {
        if (filename.empty()) {
            return std::format("line {}, col {}", line, column);
        }
        return std::format("{}:{}:{}", filename, line, column);
    }

    [[nodiscard]] bool is(TokenType t) const { return type == t; }

    template <typename... Types> [[nodiscard]] bool is_any(Types... types) const {
        return ((type == types) || ...);
    }
};

inline const std::unordered_map<std::string, TokenType>& getKeywordMap() {
    static const std::unordered_map<std::string, TokenType> keywords = {
        {"label", TokenType::KW_LABEL},
        {"jump", TokenType::KW_JUMP},
        {"end", TokenType::KW_END},

        {"scene", TokenType::KW_SCENE},
        {"show", TokenType::KW_SHOW},
        {"hide", TokenType::KW_HIDE},
        {"with", TokenType::KW_WITH},
        {"at", TokenType::KW_AT},
        {"wait", TokenType::KW_WAIT},

        {"play", TokenType::KW_PLAY},
        {"stop", TokenType::KW_STOP},
        {"bgm", TokenType::KW_BGM},
        {"sfx", TokenType::KW_SFX},
        {"voice", TokenType::KW_VOICE},
        {"menu", TokenType::KW_MENU},

        {"character", TokenType::KW_CHARACTER},
        {"set", TokenType::KW_SET},
        {"if", TokenType::KW_IF},
        {"elif", TokenType::KW_ELIF},
        {"else", TokenType::KW_ELSE},
        {"and", TokenType::KW_AND},
        {"or", TokenType::KW_OR},
        {"not", TokenType::KW_NOT},
        {"true", TokenType::KW_TRUE},
        {"false", TokenType::KW_FALSE},

        {"config", TokenType::KW_CONFIG},
        {"tween", TokenType::KW_TWEEN},
        {"emotion", TokenType::KW_EMOTION},

        {"input", TokenType::KW_INPUT},
        {"meta", TokenType::KW_META},
        {"particle", TokenType::KW_PARTICLE},

        {"screen", TokenType::KW_SCREEN},
        {"panel", TokenType::KW_PANEL},
        {"text", TokenType::KW_TEXT},
        {"imagebutton", TokenType::KW_IMAGEBUTTON},
    };
    return keywords;
}

[[nodiscard]] inline std::string_view tokenTypeToString(TokenType type) {
    switch (type) {
    case TokenType::STRING_LITERAL:
        return "STRING";
    case TokenType::NUMBER_LITERAL:
        return "NUMBER";
    case TokenType::IDENTIFIER:
        return "IDENTIFIER";
    case TokenType::COLOR_LITERAL:
        return "COLOR";
    case TokenType::KW_LABEL:
        return "LABEL";
    case TokenType::KW_JUMP:
        return "JUMP";
    case TokenType::KW_END:
        return "END";
    case TokenType::KW_SCENE:
        return "SCENE";
    case TokenType::KW_SHOW:
        return "SHOW";
    case TokenType::KW_HIDE:
        return "HIDE";
    case TokenType::KW_WITH:
        return "WITH";
    case TokenType::KW_AT:
        return "AT";
    case TokenType::KW_WAIT:
        return "WAIT";
    case TokenType::KW_PLAY:
        return "PLAY";
    case TokenType::KW_STOP:
        return "STOP";
    case TokenType::KW_BGM:
        return "BGM";
    case TokenType::KW_SFX:
        return "SFX";
    case TokenType::KW_VOICE:
        return "VOICE";
    case TokenType::KW_MENU:
        return "MENU";
    case TokenType::KW_CHARACTER:
        return "CHARACTER";
    case TokenType::KW_SET:
        return "SET";
    case TokenType::KW_IF:
        return "IF";
    case TokenType::KW_ELIF:
        return "ELIF";
    case TokenType::KW_ELSE:
        return "ELSE";
    case TokenType::KW_AND:
        return "AND";
    case TokenType::KW_OR:
        return "OR";
    case TokenType::KW_NOT:
        return "NOT";
    case TokenType::KW_TRUE:
        return "TRUE";
    case TokenType::KW_FALSE:
        return "FALSE";
    case TokenType::KW_CONFIG:
        return "CONFIG";
    case TokenType::KW_TWEEN:
        return "TWEEN";
    case TokenType::KW_EMOTION:
        return "EMOTION";
    case TokenType::KW_INPUT:
        return "INPUT";
    case TokenType::KW_META:
        return "META";
    case TokenType::KW_PARTICLE:
        return "PARTICLE";
    case TokenType::KW_SCREEN:
        return "SCREEN";
    case TokenType::KW_PANEL:
        return "PANEL";
    case TokenType::KW_TEXT:
        return "TEXT";
    case TokenType::KW_IMAGEBUTTON:
        return "IMAGEBUTTON";
    case TokenType::OP_ASSIGN:
        return "ASSIGN";
    case TokenType::OP_EQUALS:
        return "EQUALS";
    case TokenType::OP_NOT_EQUALS:
        return "NOT_EQUALS";
    case TokenType::OP_LESS:
        return "LESS";
    case TokenType::OP_GREATER:
        return "GREATER";
    case TokenType::OP_LESS_EQ:
        return "LESS_EQ";
    case TokenType::OP_GREATER_EQ:
        return "GREATER_EQ";
    case TokenType::OP_PLUS:
        return "PLUS";
    case TokenType::OP_MINUS:
        return "MINUS";
    case TokenType::OP_MULTIPLY:
        return "MULTIPLY";
    case TokenType::OP_DIVIDE:
        return "DIVIDE";
    case TokenType::OP_DOT:
        return "DOT";
    case TokenType::OP_ARROW:
        return "ARROW";
    case TokenType::COLON:
        return "COLON";
    case TokenType::COMMA:
        return "COMMA";
    case TokenType::LPAREN:
        return "LPAREN";
    case TokenType::RPAREN:
        return "RPAREN";
    case TokenType::NEWLINE:
        return "NEWLINE";
    case TokenType::EOF_TOKEN:
        return "EOF";
    default:
        return "UNKNOWN";
    }
}

} // namespace velora::script
