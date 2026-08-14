
#include "Parser.h"
#include <algorithm>

namespace velora::script {

Parser::Parser(std::vector<Token> tokens, std::string filename)
    : m_tokens(std::move(tokens)), m_filename(std::move(filename)) {}

const Token& Parser::current() const {
    if (m_pos >= m_tokens.size())
        return m_tokens.back();
    return m_tokens[m_pos];
}

const Token& Parser::peek(int offset) const {
    size_t idx = m_pos + offset;
    if (idx >= m_tokens.size())
        return m_tokens.back();
    return m_tokens[idx];
}

const Token& Parser::advance() {
    const Token& t = current();
    if (m_pos < m_tokens.size())
        m_pos++;
    return t;
}

bool Parser::match(TokenType type) {
    if (current().is(type)) {
        advance();
        return true;
    }
    return false;
}

const Token& Parser::expect(TokenType type, const std::string& ctx) {
    if (current().is(type))
        return advance();
    error(std::format("Expected {} in {}, got '{}' ({})", tokenTypeToString(type), ctx,
                      current().value, tokenTypeToString(current().type)));
}

void Parser::skipNewlines() {
    while (current().is(TokenType::NEWLINE))
        advance();
}

bool Parser::isAtEnd() const {
    return current().is(TokenType::EOF_TOKEN);
}

void Parser::error(const std::string& msg) const {
    throw ParseError(msg, current());
}

Program Parser::parse() {
    Program prog;
    skipNewlines();
    while (!isAtEnd()) {
        auto stmt = parseStatement();
        if (auto* label = std::get_if<LabelStmt>(&stmt.data)) {
            prog.labelIndex[label->name] = prog.statements.size();
        }
        prog.statements.push_back(std::move(stmt));
        skipNewlines();
    }
    return prog;
}

Statement Parser::parseStatement() {
    skipNewlines();
    const Token& tok = current();

    switch (tok.type) {
    case TokenType::KW_CHARACTER:
        return parseCharacterDef();
    case TokenType::KW_LABEL:
        return parseLabelStmt();
    case TokenType::KW_SCENE:
        return parseSceneCmd();
    case TokenType::KW_SHOW:
        return parseShowCmd();
    case TokenType::KW_HIDE:
        return parseHideCmd();
    case TokenType::KW_PLAY:
        return parsePlayCmd();
    case TokenType::KW_STOP:
        return parseStopCmd();
    case TokenType::KW_WAIT:
        return parseWaitCmd();
    case TokenType::KW_SET:
        return parseSetCmd();
    case TokenType::KW_JUMP:
        return parseJumpCmd();
    case TokenType::KW_MENU:
        return parseMenuCmd();
    case TokenType::KW_IF:
        return parseIfStmt();
    case TokenType::KW_CONFIG:
        return parseConfigCmd();
    case TokenType::KW_TWEEN:
        return parseTweenCmd();
    case TokenType::KW_EMOTION:
        return parseEmotionCmd();
    case TokenType::KW_SCREEN:
        return parseScreenDef();
    case TokenType::KW_INPUT:
        return parseInputCmd();
    case TokenType::KW_META:
        return parseMetaCmd();
    case TokenType::KW_PARTICLE:
        return parseParticleCmd();
    case TokenType::IDENTIFIER:
        if (tok.value == "show_screen")
            return parseScreenShowCmd();
        if (tok.value == "hide_screen")
            return parseScreenHideCmd();
        return parseSayCmd(tok.value);
    case TokenType::STRING_LITERAL:
        return parseNarratorCmd();
    default:
        error(std::format("Unexpected token: '{}' ({})", tok.value, tokenTypeToString(tok.type)));
    }
}

Statement Parser::parseCharacterDef() {
    int ln = current().line;
    advance();
    std::string id = expect(TokenType::IDENTIFIER, "character def").value;
    expect(TokenType::OP_ASSIGN, "character def");
    std::string name = expect(TokenType::STRING_LITERAL, "character name").value;

    std::string color = "#FFFFFF";
    if (current().is(TokenType::IDENTIFIER) && current().value == "color") {
        advance();
        expect(TokenType::COLON, "character color");
        color = expect(TokenType::COLOR_LITERAL, "color value").value;
    }

    Statement stmt;
    stmt.data = CharacterDef{id, name, color};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseLabelStmt() {
    int ln = current().line;
    advance();
    std::string name = expect(TokenType::IDENTIFIER, "label").value;
    expect(TokenType::COLON, "label");

    Statement stmt;
    stmt.data = LabelStmt{name};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseSceneCmd() {
    int ln = current().line;
    advance();
    std::string path = expect(TokenType::STRING_LITERAL, "scene image").value;

    std::optional<TransitionSpec> trans;
    if (match(TokenType::KW_WITH))
        trans = parseTransition();

    Statement stmt;
    stmt.data = SceneCmd{path, trans};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseShowCmd() {
    int ln = current().line;
    advance();
    std::string target = expect(TokenType::IDENTIFIER, "show target").value;

    std::string imgPath;
    if (current().is(TokenType::STRING_LITERAL)) {
        imgPath = advance().value;
    } else if (current().is(TokenType::IDENTIFIER) && current().value != "at" &&
               current().value != "with" && current().value != "closeup" &&
               current().value != "medium" && current().value != "far" &&
               current().value != "flipped" && current().value != "zoom" &&
               current().value != "zorder" && current().value != "rotate") {
        imgPath = advance().value;
        while (current().is(TokenType::IDENTIFIER) && current().value != "at" &&
               current().value != "with" && current().value != "closeup" &&
               current().value != "medium" && current().value != "far" &&
               current().value != "flipped" && current().value != "zoom" &&
               current().value != "zorder" && current().value != "rotate") {
            imgPath += "_" + advance().value;
        }
    }

    std::optional<PositionSpec> pos;
    if (match(TokenType::KW_AT))
        pos = parsePosition();

    std::optional<TransitionSpec> trans;
    if (match(TokenType::KW_WITH))
        trans = parseTransition();

    bool flipped = false;
    int zorder = 0;
    float zoom = 1.0f;
    float rotate = 0.0f;

    bool found = true;
    while (found) {
        found = false;
        auto z = parseOptionalFuncArg("zoom");
        if (z) {
            zoom = *z;
            found = true;
            continue;
        }
        auto zo = parseOptionalFuncArg("zorder");
        if (zo) {
            zorder = static_cast<int>(*zo);
            found = true;
            continue;
        }
        auto r = parseOptionalFuncArg("rotate");
        if (r) {
            rotate = *r;
            found = true;
            continue;
        }
        if (current().is(TokenType::IDENTIFIER) && current().value == "flipped") {
            advance();
            flipped = true;
            found = true;
            continue;
        }
        if (current().is(TokenType::IDENTIFIER)) {
            if (current().value == "closeup") {
                advance();
                zoom = 1.5f;
                found = true;
                continue;
            }
            if (current().value == "medium") {
                advance();
                zoom = 1.0f;
                found = true;
                continue;
            }
            if (current().value == "far") {
                advance();
                zoom = 0.6f;
                found = true;
                continue;
            }
        }
    }

    ShowCmd cmd;
    cmd.target = target;
    cmd.imagePath = imgPath;
    cmd.position = pos;
    cmd.transition = trans;
    cmd.flipped = flipped;
    cmd.zorder = zorder;
    cmd.zoom = zoom;
    cmd.rotate = rotate;

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseHideCmd() {
    int ln = current().line;
    advance();

    HideCmd command;
    if (current().is(TokenType::IDENTIFIER) && current().value == "all") {
        advance();
        command.hideAll = true;
    } else {
        command.target = expect(TokenType::IDENTIFIER, "hide target").value;
    }

    std::optional<TransitionSpec> trans;
    if (match(TokenType::KW_WITH))
        trans = parseTransition();
    command.transition = std::move(trans);

    Statement stmt;
    stmt.data = std::move(command);
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parsePlayCmd() {
    int ln = current().line;
    advance();

    std::string channel = "sfx";
    if (current().is_any(TokenType::KW_BGM, TokenType::KW_SFX, TokenType::KW_VOICE)) {
        channel = advance().value;
    }

    std::string path = expect(TokenType::STRING_LITERAL, "audio file").value;

    PlayCmd cmd;
    cmd.channel = channel;
    cmd.filePath = path;

    bool found = true;
    while (found) {
        found = false;
        if (!cmd.fadeIn) {
            auto v = parseOptionalFuncArg("fadein");
            if (v) {
                cmd.fadeIn = v;
                found = true;
                continue;
            }
        }
        if (!cmd.fadeOut) {
            auto v = parseOptionalFuncArg("fadeout");
            if (v) {
                cmd.fadeOut = v;
                found = true;
                continue;
            }
        }
        if (!cmd.volume) {
            auto v = parseOptionalFuncArg("volume");
            if (v) {
                cmd.volume = v;
                found = true;
                continue;
            }
        }
        if (!cmd.crossfade) {
            auto v = parseOptionalFuncArg("crossfade");
            if (v) {
                cmd.crossfade = v;
                found = true;
                continue;
            }
        }
    }

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseStopCmd() {
    int ln = current().line;
    advance();
    std::string channel = "bgm";
    if (current().is_any(TokenType::KW_BGM, TokenType::KW_SFX, TokenType::KW_VOICE))
        channel = advance().value;

    StopCmd cmd;
    cmd.channel = channel;
    cmd.fadeOut = parseOptionalFuncArg("fadeout");

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseWaitCmd() {
    int ln = current().line;
    advance();
    std::string val = expect(TokenType::NUMBER_LITERAL, "wait duration").value;
    if (!val.empty() && val.back() == 's')
        val.pop_back();

    Statement stmt;
    stmt.data = WaitCmd{std::stod(val)};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseSetCmd() {
    int ln = current().line;
    advance();
    std::string varName = expect(TokenType::IDENTIFIER, "variable name").value;
    while (match(TokenType::OP_DOT)) {
        varName += ".";
        varName += expect(TokenType::IDENTIFIER, "variable member").value;
    }
    expect(TokenType::OP_ASSIGN, "set assignment");
    Expression val = parseExpression();

    Statement stmt;
    stmt.data = SetCmd{varName, val};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseJumpCmd() {
    int ln = current().line;
    advance();
    std::string label = expect(TokenType::IDENTIFIER, "jump target").value;
    Statement stmt;
    stmt.data = JumpCmd{label};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseMenuCmd() {
    int ln = current().line;
    advance();
    expect(TokenType::COLON, "menu");
    skipNewlines();

    MenuCmd cmd;
    while (!isAtEnd() && !current().is(TokenType::KW_END)) {
        skipNewlines();
        if (current().is(TokenType::KW_END))
            break;

        std::string text = expect(TokenType::STRING_LITERAL, "choice text").value;
        expect(TokenType::COLON, "choice");
        skipNewlines();

        ChoiceOption opt;
        opt.text = text;
        opt.body = parseBlock();
        cmd.choices.push_back(std::move(opt));
    }
    expect(TokenType::KW_END, "menu block");

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseIfStmt() {
    int ln = current().line;
    advance();
    Expression cond = parseExpression();
    expect(TokenType::COLON, "if condition");
    skipNewlines();

    IfStmt ifCmd;
    ifCmd.condition = cond;
    ifCmd.thenBody = parseBlock();

    while (current().is(TokenType::KW_ELIF)) {
        advance();
        Expression elifCond = parseExpression();
        expect(TokenType::COLON, "elif condition");
        skipNewlines();
        auto elifBody = parseBlock();
        ifCmd.elifBranches.emplace_back(elifCond, std::move(elifBody));
    }

    if (match(TokenType::KW_ELSE)) {
        expect(TokenType::COLON, "else");
        skipNewlines();
        ifCmd.elseBody = parseBlock();
    }

    expect(TokenType::KW_END, "if block");

    Statement stmt;
    stmt.data = ifCmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseConfigCmd() {
    int ln = current().line;
    advance();
    std::string key = expect(TokenType::IDENTIFIER, "config key").value;
    while (match(TokenType::OP_DOT)) {
        key += ".";
        key += expect(TokenType::IDENTIFIER, "config key").value;
    }
    expect(TokenType::OP_ASSIGN, "config");
    Expression val = parseExpression();

    Statement stmt;
    stmt.data = ConfigCmd{key, val};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseTweenCmd() {
    int ln = current().line;
    advance();
    std::string target = expect(TokenType::IDENTIFIER, "tween target").value;
    std::string prop = expect(TokenType::IDENTIFIER, "tween property").value;
    Expression toVal = parseExpression();

    double dur = 1.0;
    std::string easing = "ease_out";
    auto durOpt = parseOptionalFuncArg("duration");
    if (durOpt)
        dur = *durOpt;
    if (current().is(TokenType::IDENTIFIER) && current().value != "duration") {
        easing = advance().value;
    }

    Statement stmt;
    stmt.data = TweenCmd{target, prop, toVal, dur, easing};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseEmotionCmd() {
    int ln = current().line;
    advance();
    std::string character = expect(TokenType::IDENTIFIER, "emotion character").value;
    std::string emotion = expect(TokenType::IDENTIFIER, "emotion type").value;

    Statement stmt;
    stmt.data = EmotionCmd{character, emotion};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseSayCmd(const std::string& speaker) {
    int ln = current().line;
    advance();
    std::string text = expect(TokenType::STRING_LITERAL, "dialogue text").value;

    Statement stmt;
    stmt.data = SayCmd{speaker, text};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseNarratorCmd() {
    int ln = current().line;
    std::string text = current().value;
    advance();

    Statement stmt;
    stmt.data = SayCmd{"", text};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

TransitionSpec Parser::parseTransition() {
    TransitionSpec spec;
    spec.name = expect(TokenType::IDENTIFIER, "transition name").value;
    if (match(TokenType::LPAREN)) {
        std::string val = expect(TokenType::NUMBER_LITERAL, "transition duration").value;
        if (!val.empty() && val.back() == 's')
            val.pop_back();
        spec.duration = std::stod(val);
        expect(TokenType::RPAREN, "transition args");
    }
    return spec;
}

PositionSpec Parser::parsePosition() {
    PositionSpec pos;
    if (current().is(TokenType::IDENTIFIER)) {
        pos.preset = advance().value;
        if (pos.preset == "center") {
            pos.x = 0.5f;
            pos.y = 0.5f;
        } else if (pos.preset == "left") {
            pos.x = 0.25f;
            pos.y = 0.5f;
        } else if (pos.preset == "right") {
            pos.x = 0.75f;
            pos.y = 0.5f;
        }
    } else if (current().is(TokenType::NUMBER_LITERAL)) {
        pos.x = std::stof(advance().value);
        if (match(TokenType::COMMA)) {
            pos.y = std::stof(expect(TokenType::NUMBER_LITERAL, "y position").value);
        }
    }
    return pos;
}

Expression Parser::parseExpression() {
    return parseOrExpression();
}

Expression Parser::parseOrExpression() {
    Expression left = parseAndExpression();

    while (match(TokenType::KW_OR)) {
        Expression expression;
        expression.type = Expression::Type::BINARY_OP;
        expression.op = "or";
        expression.children.push_back(std::move(left));
        expression.children.push_back(parseAndExpression());
        left = std::move(expression);
    }
    return left;
}

Expression Parser::parseAndExpression() {
    Expression left = parseComparisonExpression();

    while (match(TokenType::KW_AND)) {
        Expression expression;
        expression.type = Expression::Type::BINARY_OP;
        expression.op = "and";
        expression.children.push_back(std::move(left));
        expression.children.push_back(parseComparisonExpression());
        left = std::move(expression);
    }
    return left;
}

Expression Parser::parseComparisonExpression() {
    Expression left = parseAdditiveExpression();

    while (current().is_any(TokenType::OP_EQUALS, TokenType::OP_NOT_EQUALS, TokenType::OP_LESS,
                            TokenType::OP_GREATER, TokenType::OP_LESS_EQ,
                            TokenType::OP_GREATER_EQ)) {
        Expression expression;
        expression.type = Expression::Type::BINARY_OP;
        expression.op = advance().value;
        expression.children.push_back(std::move(left));
        expression.children.push_back(parseAdditiveExpression());
        left = std::move(expression);
    }
    return left;
}

Expression Parser::parseAdditiveExpression() {
    Expression left = parseMultiplicativeExpression();

    while (current().is_any(TokenType::OP_PLUS, TokenType::OP_MINUS)) {
        Expression expression;
        expression.type = Expression::Type::BINARY_OP;
        expression.op = advance().value;
        expression.children.push_back(std::move(left));
        expression.children.push_back(parseMultiplicativeExpression());
        left = std::move(expression);
    }
    return left;
}

Expression Parser::parseMultiplicativeExpression() {
    Expression left = parseUnaryExpression();

    while (current().is_any(TokenType::OP_MULTIPLY, TokenType::OP_DIVIDE)) {
        Expression expression;
        expression.type = Expression::Type::BINARY_OP;
        expression.op = advance().value;
        expression.children.push_back(std::move(left));
        expression.children.push_back(parseUnaryExpression());
        left = std::move(expression);
    }
    return left;
}

Expression Parser::parseUnaryExpression() {
    if (current().is_any(TokenType::KW_NOT, TokenType::OP_MINUS)) {
        Expression expression;
        expression.type = Expression::Type::UNARY_OP;
        expression.op = advance().value;
        expression.children.push_back(parseUnaryExpression());
        return expression;
    }
    return parsePrimary();
}

Expression Parser::parsePrimary() {
    const Token& tok = current();
    Expression expr;

    if (tok.is(TokenType::LPAREN)) {
        advance();
        expr = parseExpression();
        expect(TokenType::RPAREN, "parenthesized expression");
    } else if (tok.is(TokenType::STRING_LITERAL)) {
        expr.type = Expression::Type::STRING;
        expr.value = advance().value;
    } else if (tok.is(TokenType::NUMBER_LITERAL)) {
        expr.type = Expression::Type::NUMBER;
        expr.value = advance().value;
    } else if (tok.is(TokenType::KW_TRUE)) {
        expr.type = Expression::Type::BOOLEAN;
        expr.value = "true";
        advance();
    } else if (tok.is(TokenType::KW_FALSE)) {
        expr.type = Expression::Type::BOOLEAN;
        expr.value = "false";
        advance();
    } else if (tok.is(TokenType::COLOR_LITERAL)) {
        expr.type = Expression::Type::COLOR;
        expr.value = advance().value;
    } else if (tok.is(TokenType::IDENTIFIER)) {
        expr.type = Expression::Type::IDENTIFIER;
        expr.value = advance().value;
        while (current().is(TokenType::OP_DOT)) {
            advance();
            expr.value += ".";
            expr.value += expect(TokenType::IDENTIFIER, "member access").value;
            expr.type = Expression::Type::VARIABLE_REF;
        }
    } else {
        error(std::format("Expected expression, got '{}' ({})", tok.value,
                          tokenTypeToString(tok.type)));
    }
    return expr;
}

std::vector<Statement> Parser::parseBlock() {
    std::vector<Statement> body;
    skipNewlines();
    while (!isAtEnd() && !current().is(TokenType::KW_END) && !current().is(TokenType::KW_ELIF) &&
           !current().is(TokenType::KW_ELSE) &&
           !(current().is(TokenType::STRING_LITERAL) && peek().is(TokenType::COLON))) {
        body.push_back(parseStatement());
        skipNewlines();
    }
    return body;
}

std::optional<double> Parser::parseOptionalFuncArg(const std::string& name) {
    if (current().value != name) {
        return std::nullopt;
    }

    advance();
    expect(TokenType::LPAREN, name + " args");
    const bool negative = match(TokenType::OP_MINUS);
    std::string value = expect(TokenType::NUMBER_LITERAL, name + " value").value;
    if (!value.empty() && value.back() == 's') {
        value.pop_back();
    }
    expect(TokenType::RPAREN, name + " args");
    const double number = std::stod(value);
    return negative ? -number : number;
}

ScreenElement Parser::parseScreenElement() {
    ScreenElement el;

    if (current().is(TokenType::KW_TEXT)) {
        advance();
        el.type = "text";
        el.text = expect(TokenType::STRING_LITERAL, "screen text").value;
    } else if (current().is(TokenType::KW_IMAGEBUTTON)) {
        advance();
        el.type = "imagebutton";
        el.imagePath = expect(TokenType::STRING_LITERAL, "imagebutton image").value;
    } else if (current().is(TokenType::KW_PANEL)) {
        advance();
        el.type = "panel";
    } else {
        error("Expected screen element: text, imagebutton, or panel");
    }

    bool parsing = true;
    while (parsing) {
        parsing = false;
        if (match(TokenType::KW_AT)) {
            el.x = std::stof(expect(TokenType::NUMBER_LITERAL, "element x").value);
            if (match(TokenType::COMMA)) {
                el.y = std::stof(expect(TokenType::NUMBER_LITERAL, "element y").value);
            }
            parsing = true;
            continue;
        }
        if (current().is(TokenType::IDENTIFIER) && current().value == "size") {
            advance();
            expect(TokenType::LPAREN, "size");
            el.width = std::stof(expect(TokenType::NUMBER_LITERAL, "width").value);
            if (match(TokenType::COMMA)) {
                el.height = std::stof(expect(TokenType::NUMBER_LITERAL, "height").value);
            }
            expect(TokenType::RPAREN, "size");
            parsing = true;
            continue;
        }
        if (current().is(TokenType::IDENTIFIER) && current().value == "color") {
            advance();
            expect(TokenType::COLON, "color");
            el.color = expect(TokenType::COLOR_LITERAL, "color value").value;
            parsing = true;
            continue;
        }
        auto a = parseOptionalFuncArg("alpha");
        if (a) {
            el.alpha = static_cast<float>(*a);
            parsing = true;
            continue;
        }
        if (current().is(TokenType::IDENTIFIER) && current().value == "action") {
            advance();
            if (current().is(TokenType::KW_JUMP)) {
                advance();
                el.action = "jump:" + expect(TokenType::IDENTIFIER, "action target").value;
            } else {
                el.action = expect(TokenType::IDENTIFIER, "action").value;
            }
            parsing = true;
            continue;
        }
    }

    if (el.type == "panel" && match(TokenType::COLON)) {
        skipNewlines();
        while (!isAtEnd() && !current().is(TokenType::KW_END)) {
            skipNewlines();
            if (current().is(TokenType::KW_END))
                break;
            el.children.push_back(parseScreenElement());
            skipNewlines();
        }
        expect(TokenType::KW_END, "panel block");
    }

    return el;
}

Statement Parser::parseScreenDef() {
    int ln = current().line;
    advance();
    std::string name = expect(TokenType::IDENTIFIER, "screen name").value;
    expect(TokenType::COLON, "screen");
    skipNewlines();

    ScreenDef def;
    def.name = name;
    while (!isAtEnd() && !current().is(TokenType::KW_END)) {
        skipNewlines();
        if (current().is(TokenType::KW_END))
            break;
        def.elements.push_back(parseScreenElement());
        skipNewlines();
    }
    expect(TokenType::KW_END, "screen block");

    Statement stmt;
    stmt.data = def;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseScreenShowCmd() {
    int ln = current().line;
    advance();
    std::string name = expect(TokenType::IDENTIFIER, "screen name").value;
    Statement stmt;
    stmt.data = ScreenShowCmd{name};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseScreenHideCmd() {
    int ln = current().line;
    advance();
    std::string name = expect(TokenType::IDENTIFIER, "screen name").value;
    Statement stmt;
    stmt.data = ScreenHideCmd{name};
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseInputCmd() {
    int ln = current().line;
    advance();

    InputCmd cmd;
    cmd.prompt = expect(TokenType::STRING_LITERAL, "input prompt").value;

    expect(TokenType::OP_ARROW, "input arrow '->'");
    cmd.variable = expect(TokenType::IDENTIFIER, "input variable").value;
    while (current().is(TokenType::OP_DOT)) {
        advance();
        cmd.variable += "." + expect(TokenType::IDENTIFIER, "input variable part").value;
    }

    skipNewlines();
    if (current().is(TokenType::STRING_LITERAL)) {
        cmd.inputType = "choice";
        while (current().is(TokenType::STRING_LITERAL)) {
            cmd.options.push_back(advance().value);
            skipNewlines();
        }
        expect(TokenType::KW_END, "input choice 'end'");
    } else {
        cmd.inputType = "text";
    }

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseMetaCmd() {
    int ln = current().line;
    advance();

    MetaCmd cmd;
    cmd.action = expect(TokenType::IDENTIFIER, "meta action").value;

    while (!current().is(TokenType::NEWLINE) && !current().is(TokenType::EOF_TOKEN)) {
        if (current().is(TokenType::STRING_LITERAL)) {
            cmd.args.push_back(advance().value);
        } else if (current().is(TokenType::NUMBER_LITERAL)) {
            cmd.args.push_back(advance().value);
        } else if (current().is(TokenType::IDENTIFIER)) {
            cmd.args.push_back(advance().value);
        } else if (current().is(TokenType::COLOR_LITERAL)) {
            cmd.args.push_back(advance().value);
        } else {
            break;
        }
    }

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

Statement Parser::parseParticleCmd() {
    int ln = current().line;
    advance();

    ParticleCmd cmd;
    Token presetTok = advance();
    cmd.preset = presetTok.value;

    if (cmd.preset == "stop") {
        if (current().is(TokenType::IDENTIFIER) && current().value == "all") {
            advance();
            cmd.stopAll = true;
        } else if (current().is(TokenType::STRING_LITERAL)) {
            cmd.name = advance().value;
        } else if (current().is(TokenType::IDENTIFIER)) {
            cmd.name = advance().value;
        }
    } else {
        if (cmd.preset == "custom" && current().is(TokenType::STRING_LITERAL)) {
            cmd.name = advance().value;
        }

        while (!current().is(TokenType::NEWLINE) && !current().is(TokenType::EOF_TOKEN)) {
            if (current().is_any(TokenType::IDENTIFIER, TokenType::KW_AT)) {
                std::string param = current().value;
                advance();
                if (current().is(TokenType::LPAREN)) {
                    advance();
                    if (param == "intensity") {
                        cmd.intensity =
                            std::stof(expect(TokenType::NUMBER_LITERAL, "intensity value").value);
                    } else if (param == "wind") {
                        cmd.wind = std::stof(expect(TokenType::NUMBER_LITERAL, "wind value").value);
                    } else if (param == "at") {
                        cmd.x = std::stof(expect(TokenType::NUMBER_LITERAL, "x position").value);
                        if (current().is(TokenType::COMMA))
                            advance();
                        cmd.y = std::stof(expect(TokenType::NUMBER_LITERAL, "y position").value);
                    } else if (param == "radius") {
                        cmd.radius =
                            std::stof(expect(TokenType::NUMBER_LITERAL, "radius value").value);
                    }
                    expect(TokenType::RPAREN, ")");
                }
            } else {
                break;
            }
        }
    }

    Statement stmt;
    stmt.data = cmd;
    stmt.line = ln;
    stmt.file = m_filename;
    return stmt;
}

} // namespace velora::script
