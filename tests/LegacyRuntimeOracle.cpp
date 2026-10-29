#include "script/Lexer.h"
#include "script/Parser.h"
#include "script/ScriptVM.h"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <variant>
#include <vector>

#include <nlohmann/json.hpp>

namespace fs = std::filesystem;
using json = nlohmann::json;

namespace {

class ScopedOutputCapture {
  public:
    ScopedOutputCapture()
        : m_previousOut(std::cout.rdbuf(m_output.rdbuf())),
          m_previousError(std::cerr.rdbuf(m_error.rdbuf())) {}

    ~ScopedOutputCapture() {
        std::cout.rdbuf(m_previousOut);
        std::cerr.rdbuf(m_previousError);
    }

    ScopedOutputCapture(const ScopedOutputCapture&) = delete;
    ScopedOutputCapture& operator=(const ScopedOutputCapture&) = delete;

  private:
    std::ostringstream m_output;
    std::ostringstream m_error;
    std::streambuf* m_previousOut;
    std::streambuf* m_previousError;
};

velora::script::Program parseProgram(const std::string& source, const std::string& name) {
    velora::script::Lexer lexer(source, name);
    velora::script::Parser parser(lexer.tokenize(), name);
    return parser.parse();
}

std::string stateName(velora::script::VMState state) {
    using velora::script::VMState;
    switch (state) {
    case VMState::RUNNING:
        return "running";
    case VMState::WAITING_INPUT:
        return "waiting_input";
    case VMState::WAITING_CHOICE:
        return "waiting_choice";
    case VMState::WAITING_TIMED:
        return "waiting_timed";
    case VMState::WAITING_TEXT_INPUT:
        return "waiting_text_input";
    case VMState::FINISHED:
        return "finished";
    }
    throw std::runtime_error("unknown legacy VM state");
}

json valueToJson(const velora::script::VarValue& value) {
    return std::visit([](const auto& item) -> json { return item; }, value);
}

json capture(const velora::script::ScriptVM& vm, const std::vector<std::string>& variables) {
    json values = json::object();
    for (const std::string& name : variables) {
        values[name] = vm.hasVariable(name) ? valueToJson(vm.getVariable(name)) : json(nullptr);
    }

    return {
        {"state", stateName(vm.getState())},
        {"statement_cursor", vm.getCurrentLine()},
        {"current_label", vm.getCurrentLabel()},
        {"variables", std::move(values)},
    };
}

json preStartTrace() {
    const std::string source = R"(
character mira = "Mira" color: #ACCC9D
set seeded = 2
config skip_effects = true
screen notice:
    text "Ready" at 0.1,0.1
end
meta lock_save
label start:
    set started = 1
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "pre-start.vel"));

    const auto* character = vm.getCharacter("mira");
    json loaded = capture(vm, {"seeded", "__config.skip_effects", "started"});
    loaded["has_os_username"] = vm.hasVariable("sys.os_username");
    loaded["registered_character"] = character == nullptr
                                         ? json(nullptr)
                                         : json{{"display_name", character->displayName},
                                                {"color", character->color}};
    loaded["active_screen_count"] = vm.getActiveScreens().size();

    vm.update(0.0);
    return {{"after_load", std::move(loaded)},
            {"after_first_update", capture(vm, {"seeded", "started"})}};
}

json nestedIfPauseTrace() {
    const std::string source = R"(
label start:
    if true:
        "Nested dialogue"
        set branch_after_dialogue = 1
    end
    set outer_after_if = 1
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "nested-if-pause.vel"));
    vm.update(0.0);
    json paused = capture(vm, {"branch_after_dialogue", "outer_after_if"});
    vm.advance();
    return {{"after_update", std::move(paused)},
            {"after_advance", capture(vm, {"branch_after_dialogue", "outer_after_if"})}};
}

json nestedWaitTrace() {
    const std::string source = R"(
label start:
    if true:
        wait 2.0
        set branch_after_wait = 1
    end
    set outer_after_if = 1
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "nested-wait.vel"));
    vm.update(0.0);
    json started = capture(vm, {"branch_after_wait", "outer_after_if"});
    vm.update(1.25);
    json partial = capture(vm, {"branch_after_wait", "outer_after_if"});
    vm.update(0.75);
    return {{"after_update", std::move(started)},
            {"after_1_25_seconds", std::move(partial)},
            {"after_2_seconds", capture(vm, {"branch_after_wait", "outer_after_if"})}};
}

json choiceContinuationTrace() {
    const std::string source = R"(
label start:
    menu:
        "First":
            "Nested choice dialogue"
            set choice_after_dialogue = 1
        "Second":
            set second_choice = 1
    end
    set outer_after_menu = 1
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "choice-continuation.vel"));
    vm.update(0.0);
    json offered = capture(vm, {"choice_after_dialogue", "outer_after_menu"});
    vm.selectChoice(-1);
    json invalid = capture(vm, {"choice_after_dialogue", "outer_after_menu"});
    vm.selectChoice(0);
    json selected = capture(vm, {"choice_after_dialogue", "outer_after_menu"});
    vm.update(0.0);
    return {{"after_update", std::move(offered)},
            {"after_invalid_choice", std::move(invalid)},
            {"after_first_choice", std::move(selected)},
            {"after_next_update", capture(vm, {"choice_after_dialogue", "outer_after_menu"})}};
}

json inputTrace() {
    const std::string source = R"(
label start:
    input "Route" -> route
        "Alpha"
        "Beta"
    end
    set after_input = 1
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "input-choice.vel"));
    vm.update(0.0);
    const std::string prompt = vm.getTextInputPrompt();
    json offered = capture(vm, {"route", "after_input", "sys.name_gibberish"});
    vm.selectChoice(1);
    json selected = capture(vm, {"route", "after_input", "sys.name_gibberish"});
    vm.update(0.0);
    return {{"prompt", prompt},
            {"after_update", std::move(offered)},
            {"after_selection", std::move(selected)},
            {"after_next_update", capture(vm, {"route", "after_input", "sys.name_gibberish"})}};
}

json coercionTrace() {
    const std::string source = R"(
label start:
    set numeric_text_add = "2.5" + 1
    set boolean_add = true + 2
    set number_text_equal = 1 == "1"
    set normalized_number_text_equal = 1 == "1.000000"
    set boolean_text_equal = true == "true"
    set missing_equal_empty = missing == ""
    set divide_zero = 7 / 0
    set unary_bad = -"oops"
    if "false":
        set lowercase_false_branch = 1
    else:
        set lowercase_false_branch = 2
    end
    if "False":
        set uppercase_false_branch = 1
    else:
        set uppercase_false_branch = 2
    end
)";
    const std::vector<std::string> variables = {
        "numeric_text_add",          "boolean_add",       "number_text_equal",
        "normalized_number_text_equal", "boolean_text_equal", "missing_equal_empty",
        "divide_zero",               "unary_bad",         "lowercase_false_branch",
        "uppercase_false_branch",
    };
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "coercion.vel"));
    vm.update(0.0);
    return capture(vm, variables);
}

json flowLeniencyTrace() {
    const std::string source = R"(
label start:
    set reached = 1
label start:
    set reached = 2
    jump missing_label
    set continued_after_missing_jump = true
)";
    velora::script::ScriptVM vm(nullptr);
    vm.loadProgram(parseProgram(source, "flow-leniency.vel"));
    vm.update(0.0);
    return capture(vm, {"reached", "continued_after_missing_jump"});
}

json buildTrace() {
    ScopedOutputCapture captureOutput;
    return {
        {"schema_version", 1},
        {"oracle", "Velora C++ production ScriptVM"},
        {"pre_start", preStartTrace()},
        {"nested_if_pause", nestedIfPauseTrace()},
        {"nested_wait", nestedWaitTrace()},
        {"choice_continuation", choiceContinuationTrace()},
        {"input_choice", inputTrace()},
        {"mixed_coercion", coercionTrace()},
        {"flow_leniency", flowLeniencyTrace()},
    };
}

json readJson(const fs::path& path) {
    std::ifstream input(path);
    if (!input)
        throw std::runtime_error("could not open runtime oracle fixture: " + path.string());
    json value;
    input >> value;
    return value;
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const json actual = buildTrace();
        if (argc == 2 && std::string(argv[1]) == "--emit") {
            std::cout << actual.dump(2) << '\n';
            return 0;
        }
        if (argc == 3 && std::string(argv[1]) == "--verify") {
            const fs::path fixture = argv[2];
            const json expected = readJson(fixture);
            if (actual != expected) {
                std::cerr << "Legacy runtime oracle fixture mismatch.\nExpected:\n"
                          << expected.dump(2) << "\nActual:\n"
                          << actual.dump(2) << '\n';
                return 1;
            }
            std::cout << "Legacy runtime oracle fixture verified.\n";
            return 0;
        }

        std::cerr << "Usage: VeloraLegacyRuntimeOracle --emit | --verify <fixture.json>\n";
        return 2;
    } catch (const std::exception& error) {
        std::cerr << "Legacy runtime oracle failure: " << error.what() << '\n';
        return 1;
    }
}
