
#pragma once

#include "Token.h"
#include <string>
#include <vector>
#include <memory>
#include <variant>
#include <optional>
#include <stdexcept>
#include <unordered_map>

namespace velora::script {

class ParseError : public std::runtime_error {
  public:
    ParseError(const std::string& msg, const Token& tok)
        : std::runtime_error(std::format("[Parse Error] {} at {}", msg, tok.location())),
          m_token(tok) {}
    [[nodiscard]] const Token& token() const { return m_token; }

  private:
    Token m_token;
};

struct Expression {
    enum class Type {
        STRING,
        NUMBER,
        BOOLEAN,
        IDENTIFIER,
        VARIABLE_REF,
        COLOR,
        BINARY_OP,
        UNARY_OP
    };
    Type type;
    std::string value;
    std::string op;
    std::vector<Expression> children;
};

struct TransitionSpec {
    std::string name;
    double duration = 1.0;
};

struct PositionSpec {
    std::string preset;
    float x = 0.5f;
    float y = 0.5f;
};

struct CharacterDef {
    std::string id;
    std::string displayName;
    std::string color;
};

struct LabelStmt {
    std::string name;
};

struct SceneCmd {
    std::string imagePath;
    std::optional<TransitionSpec> transition;
};

struct ShowCmd {
    std::string target;
    std::string imagePath;
    std::optional<PositionSpec> position;
    std::optional<TransitionSpec> transition;
    bool flipped = false;
    int zorder = 0;
    float zoom = 1.0f;
    float rotate = 0.0f;
};

struct HideCmd {
    std::string target;
    bool hideAll = false;
    std::optional<TransitionSpec> transition;
};

struct InputCmd {
    std::string variable;
    std::string prompt;
    std::string inputType;
    std::vector<std::string> options;
};

struct SayCmd {
    std::string speaker;
    std::string text;
};

struct PlayCmd {
    std::string channel;
    std::string filePath;
    std::optional<double> fadeIn;
    std::optional<double> fadeOut;
    std::optional<double> volume;
    std::optional<double> crossfade;
};

struct StopCmd {
    std::string channel;
    std::optional<double> fadeOut;
};

struct WaitCmd {
    double duration;
};

struct SetCmd {
    std::string variable;
    Expression value;
};

struct JumpCmd {
    std::string label;
};

struct ChoiceOption {
    std::string text;
    std::optional<Expression> condition;
    std::vector<struct Statement> body;
};

struct MenuCmd {
    std::string prompt;
    std::vector<ChoiceOption> choices;
};

struct IfStmt {
    Expression condition;
    std::vector<struct Statement> thenBody;
    std::vector<std::pair<Expression, std::vector<struct Statement>>> elifBranches;
    std::vector<struct Statement> elseBody;
};

struct ConfigCmd {
    std::string key;
    Expression value;
};

struct TweenCmd {
    std::string target;
    std::string property;
    Expression toValue;
    double duration;
    std::string easing;
};

struct EmotionCmd {
    std::string character;
    std::string emotion;
};

struct ScreenElement {
    std::string type;
    std::string text;
    std::string imagePath;
    std::string action;
    float x = 0.5f, y = 0.5f;
    float width = 0.0f, height = 0.0f;
    float alpha = 1.0f;
    std::string color;
    std::vector<ScreenElement> children;
};

struct ScreenDef {
    std::string name;
    std::vector<ScreenElement> elements;
};

struct ScreenShowCmd {
    std::string screenName;
};

struct ScreenHideCmd {
    std::string screenName;
};

struct MetaCmd {
    std::string action;
    std::vector<std::string> args;
};

struct ParticleCmd {
    std::string preset;
    std::string name;
    float intensity = 1.0f;
    float wind = 0.0f;
    float x = 0.5f, y = 0.8f;
    float radius = 0.1f;
    bool stopAll = false;
};

struct Statement {
    using Payload = std::variant<CharacterDef, LabelStmt, SceneCmd, ShowCmd, HideCmd, SayCmd,
                                 PlayCmd, StopCmd, WaitCmd, SetCmd, JumpCmd, MenuCmd,
                                 IfStmt, ConfigCmd, TweenCmd, EmotionCmd, InputCmd, ScreenDef,
                                 ScreenShowCmd, ScreenHideCmd, MetaCmd, ParticleCmd>;
    Payload data;
    int line = 0;
    std::string file;
};

struct Program {
    std::vector<Statement> statements;
    std::unordered_map<std::string, size_t> labelIndex;
};

class Parser {
  public:
    Parser(std::vector<Token> tokens, std::string filename = "<input>");
    [[nodiscard]] Program parse();

  private:
    [[nodiscard]] const Token& current() const;
    [[nodiscard]] const Token& peek(int offset = 1) const;
    const Token& advance();
    bool match(TokenType type);
    const Token& expect(TokenType type, const std::string& context);
    void skipNewlines();
    [[nodiscard]] bool isAtEnd() const;
    [[noreturn]] void error(const std::string& msg) const;

    Statement parseStatement();
    Statement parseCharacterDef();
    Statement parseLabelStmt();
    Statement parseSceneCmd();
    Statement parseShowCmd();
    Statement parseHideCmd();
    Statement parsePlayCmd();
    Statement parseStopCmd();
    Statement parseWaitCmd();
    Statement parseSetCmd();
    Statement parseJumpCmd();
    Statement parseMenuCmd();
    Statement parseIfStmt();
    Statement parseConfigCmd();
    Statement parseTweenCmd();
    Statement parseEmotionCmd();
    Statement parseSayCmd(const std::string& speaker);
    Statement parseNarratorCmd();
    Statement parseWindowCmd();
    Statement parseInputCmd();
    Statement parseMetaCmd();
    Statement parseParticleCmd();
    Statement parseScreenDef();
    Statement parseScreenShowCmd();
    Statement parseScreenHideCmd();
    ScreenElement parseScreenElement();

    TransitionSpec parseTransition();
    PositionSpec parsePosition();
    Expression parseExpression();
    Expression parseOrExpression();
    Expression parseAndExpression();
    Expression parseComparisonExpression();
    Expression parseAdditiveExpression();
    Expression parseMultiplicativeExpression();
    Expression parseUnaryExpression();
    Expression parsePrimary();
    std::vector<Statement> parseBlock();

    std::optional<double> parseOptionalFuncArg(const std::string& name);

    std::vector<Token> m_tokens;
    size_t m_pos = 0;
    std::string m_filename;
};
} // namespace velora::script
