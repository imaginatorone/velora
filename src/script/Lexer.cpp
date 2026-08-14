
#include "Lexer.h"
#include <cctype>
#include <algorithm>

namespace velora::script {

Lexer::Lexer(std::string source, std::string filename)
    : m_source(std::move(source)), m_filename(std::move(filename)) {}

char Lexer::current() const {
    if (m_pos >= m_source.size())
        return '\0';
    return m_source[m_pos];
}

char Lexer::peek(int offset) const {
    size_t idx = m_pos + offset;
    if (idx >= m_source.size())
        return '\0';
    return m_source[idx];
}

char Lexer::advance() {
    char c = current();
    if (c == '\n') {
        m_line++;
        m_column = 1;
    } else {
        m_column++;
    }
    m_pos++;
    return c;
}

bool Lexer::isAtEnd() const {
    return m_pos >= m_source.size();
}

void Lexer::skipWhitespace() {
    while (!isAtEnd() && (current() == ' ' || current() == '\t' || current() == '\r')) {
        advance();
    }
}

void Lexer::skipComment() {
    while (!isAtEnd() && current() != '\n')
        advance();
}

Token Lexer::makeToken(TokenType type, const std::string& value) const {
    return Token(type, value, m_line, m_column, m_filename);
}

void Lexer::error(const std::string& message) const {
    throw LexerError(message, m_line, m_column);
}

Token Lexer::scanString() {
    int startLine = m_line, startCol = m_column;
    advance();
    std::string value;
    while (!isAtEnd() && current() != '"') {
        if (current() == '\\') {
            advance();
            switch (current()) {
            case 'n':
                value += '\n';
                break;
            case 't':
                value += '\t';
                break;
            case '\\':
                value += '\\';
                break;
            case '"':
                value += '"';
                break;
            default:
                value += current();
                break;
            }
        } else {
            value += current();
        }
        advance();
    }
    if (isAtEnd())
        error("Unterminated string literal");
    advance();
    return Token(TokenType::STRING_LITERAL, value, startLine, startCol, m_filename);
}

Token Lexer::scanNumber() {
    int startLine = m_line, startCol = m_column;
    std::string value;
    bool hasDecimal = false;

    while (!isAtEnd() && (std::isdigit(current()) || current() == '.')) {
        if (current() == '.') {
            if (hasDecimal)
                break;
            hasDecimal = true;
        }
        value += current();
        advance();
    }
    if (!isAtEnd() && current() == 's') {
        value += current();
        advance();
    }
    return Token(TokenType::NUMBER_LITERAL, value, startLine, startCol, m_filename);
}

Token Lexer::scanIdentifier() {
    int startLine = m_line, startCol = m_column;
    std::string value;
    while (!isAtEnd() && (std::isalnum(current()) || current() == '_')) {
        value += current();
        advance();
    }
    auto& keywords = getKeywordMap();
    auto it = keywords.find(value);
    if (it != keywords.end()) {
        return Token(it->second, value, startLine, startCol, m_filename);
    }
    return Token(TokenType::IDENTIFIER, value, startLine, startCol, m_filename);
}

Token Lexer::scanColor() {
    int startLine = m_line, startCol = m_column;
    std::string value;
    value += current();
    advance();
    while (!isAtEnd() && std::isxdigit(current())) {
        value += current();
        advance();
    }
    if (value.size() != 7 && value.size() != 4 && value.size() != 9) {
        error("Invalid color literal: " + value);
    }
    return Token(TokenType::COLOR_LITERAL, value, startLine, startCol, m_filename);
}

Token Lexer::nextToken() {
    if (m_hasPeeked) {
        m_hasPeeked = false;
        return m_peekedToken;
    }

    skipWhitespace();
    if (isAtEnd())
        return makeToken(TokenType::EOF_TOKEN, "");

    char c = current();

    if (c == '-' && peek() == '-') {
        skipComment();
        return nextToken();
    }
    if (c == '/' && peek() == '/') {
        skipComment();
        return nextToken();
    }
    if (c == '-' && peek() == '>') {
        int al = m_line, ac = m_column;
        advance();
        advance();
        return Token(TokenType::OP_ARROW, "->", al, ac, m_filename);
    }

    if (c == '\n') {
        advance();
        return makeToken(TokenType::NEWLINE, "\\n");
    }

    if (c == '"')
        return scanString();

    if (std::isdigit(c))
        return scanNumber();

    if (c == '#' && std::isxdigit(peek()))
        return scanColor();

    if (std::isalpha(c) || c == '_')
        return scanIdentifier();

    int ln = m_line, col = m_column;
    advance();
    switch (c) {
    case ':':
        return Token(TokenType::COLON, ":", ln, col, m_filename);
    case ',':
        return Token(TokenType::COMMA, ",", ln, col, m_filename);
    case '(':
        return Token(TokenType::LPAREN, "(", ln, col, m_filename);
    case ')':
        return Token(TokenType::RPAREN, ")", ln, col, m_filename);
    case '+':
        return Token(TokenType::OP_PLUS, "+", ln, col, m_filename);
    case '-':
        return Token(TokenType::OP_MINUS, "-", ln, col, m_filename);
    case '*':
        return Token(TokenType::OP_MULTIPLY, "*", ln, col, m_filename);
    case '/':
        return Token(TokenType::OP_DIVIDE, "/", ln, col, m_filename);
    case '.':
        return Token(TokenType::OP_DOT, ".", ln, col, m_filename);
    case '=':
        if (current() == '=') {
            advance();
            return Token(TokenType::OP_EQUALS, "==", ln, col, m_filename);
        }
        return Token(TokenType::OP_ASSIGN, "=", ln, col, m_filename);
    case '!':
        if (current() == '=') {
            advance();
            return Token(TokenType::OP_NOT_EQUALS, "!=", ln, col, m_filename);
        }
        error("Unexpected '!' - did you mean '!=' or 'not'?");
    case '<':
        if (current() == '=') {
            advance();
            return Token(TokenType::OP_LESS_EQ, "<=", ln, col, m_filename);
        }
        return Token(TokenType::OP_LESS, "<", ln, col, m_filename);
    case '>':
        if (current() == '=') {
            advance();
            return Token(TokenType::OP_GREATER_EQ, ">=", ln, col, m_filename);
        }
        return Token(TokenType::OP_GREATER, ">", ln, col, m_filename);
    default:
        error(std::string("Unexpected character: '") + c + "'");
    }
}

Token Lexer::peekToken() {
    if (!m_hasPeeked) {
        m_peekedToken = nextToken();
        m_hasPeeked = true;
    }
    return m_peekedToken;
}

std::vector<Token> Lexer::tokenize() {
    std::vector<Token> tokens;
    while (true) {
        Token t = nextToken();
        if (t.type == TokenType::NEWLINE && !tokens.empty() &&
            tokens.back().type == TokenType::NEWLINE)
            continue;
        tokens.push_back(t);
        if (t.type == TokenType::EOF_TOKEN)
            break;
    }
    return tokens;
}

} // namespace velora::script
