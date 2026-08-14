
#pragma once

#include "Token.h"

#include <string>
#include <string_view>
#include <vector>
#include <stdexcept>

namespace velora::script {

class LexerError : public std::runtime_error {
  public:
    LexerError(const std::string& message, int line, int column)
        : std::runtime_error(
              std::format("[Lexer Error] {} at line {}, col {}", message, line, column)),
          m_line(line), m_column(column) {}

    [[nodiscard]] int line() const { return m_line; }
    [[nodiscard]] int column() const { return m_column; }

  private:
    int m_line;
    int m_column;
};

class Lexer {
  public:
    explicit Lexer(std::string source, std::string filename = "<input>");

    [[nodiscard]] std::vector<Token> tokenize();

    [[nodiscard]] Token nextToken();

    [[nodiscard]] Token peekToken();

    [[nodiscard]] bool isAtEnd() const;

  private:
    [[nodiscard]] char current() const;

    [[nodiscard]] char peek(int offset = 1) const;

    char advance();


    void skipWhitespace();

    void skipComment();

    Token scanString();

    Token scanNumber();

    Token scanIdentifier();

    Token scanColor();


    Token makeToken(TokenType type, const std::string& value) const;


    [[noreturn]] void error(const std::string& message) const;

    std::string m_source;
    std::string m_filename;
    size_t m_pos = 0;
    int m_line = 1;
    int m_column = 1;

    bool m_hasPeeked = false;
    Token m_peekedToken;
};
} // namespace velora::script
