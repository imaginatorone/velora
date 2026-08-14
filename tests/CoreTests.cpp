#include "core/GameStateManager.h"
#include "script/Lexer.h"
#include "script/Parser.h"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>

#ifndef VELORA_SOURCE_DIR
#define VELORA_SOURCE_DIR "."
#endif

namespace fs = std::filesystem;

namespace {

void require(bool condition, const std::string& message) {
    if (!condition)
        throw std::runtime_error(message);
}

void testScriptPipeline() {
    const std::string source = R"(
character guide = "Guide" color:#accc9d
set visits = 0
set difference = 3-1
label start:
    scene "backgrounds/station.png" with dissolve(0.5)
    show guide "sprites/guide.png" at left zoom(1.2) zorder(3) rotate(-5) flipped
    hide all with dissolve(0.2)
    guide "Welcome."
    menu:
        "Continue":
            set visits = visits + 1
        "Leave":
            jump finish
    end
    if not (visits == 0 or visits > 2):
        "The logical expression evaluated."
    end
label finish:
    "Done."
)";
    velora::script::Lexer lexer(source, "test.vel");
    auto tokens = lexer.tokenize();
    require(!tokens.empty(), "lexer returned no tokens");
    velora::script::Parser parser(std::move(tokens), "test.vel");
    const auto program = parser.parse();
    require(program.labelIndex.contains("start"), "start label was not indexed");
    require(program.labelIndex.contains("finish"), "finish label was not indexed");
    require(program.statements.size() >= 6, "parser dropped statements");

    bool foundConfiguredSprite = false;
    bool foundHideAll = false;
    for (const auto& statement : program.statements) {
        if (const auto* show = std::get_if<velora::script::ShowCmd>(&statement.data)) {
            foundConfiguredSprite = show->flipped && show->zorder == 3 && show->zoom == 1.2f &&
                                    show->rotate == -5.0f;
        }
        if (const auto* hide = std::get_if<velora::script::HideCmd>(&statement.data)) {
            foundHideAll = hide->hideAll && hide->transition.has_value();
        }
    }
    require(foundConfiguredSprite, "sprite modifiers were not parsed");
    require(foundHideAll, "hide all was not parsed");
}

void testBundledScript() {
    const fs::path path = fs::path(VELORA_SOURCE_DIR) / "assets/scripts/example.vel";
    std::ifstream input(path);
    require(static_cast<bool>(input), "bundled script is missing");
    std::ostringstream contents;
    contents << input.rdbuf();
    velora::script::Lexer lexer(contents.str(), path.string());
    velora::script::Parser parser(lexer.tokenize(), path.string());
    const auto program = parser.parse();
    require(program.labelIndex.contains("start"), "bundled script has no start label");
    require(program.statements.size() > 30, "bundled script appears truncated");
}

void testDocumentedSyntax() {
    const std::string source = R"(
character mira = "Mira" color: #ACCC9D
screen notice:
    panel at 0.1,0.1 size(0.8,0.2) alpha(0.9):
        text "Connection restored" at 0.16,0.17 color: #EDF4E9
    end
end
label start:
    input "Difficulty" -> difficulty
        "Story"
        "Normal"
        "Hard"
    end
    scene "backgrounds/station.png" with dissolve(0.8)
    show mira "sprites/mira.png" at left with dissolve(0.4) zoom(1.1) flipped
    play bgm "audio/theme.ogg" fadein(1.0) volume(0.7)
    tween mira x 0.7 duration(0.5) ease_out
    particle fire at(0.5,0.8) radius(0.12)
    show_screen notice
    meta effect vignette 0.6 2.0
)";

    velora::script::Lexer lexer(source, "reference.vel");
    velora::script::Parser parser(lexer.tokenize(), "reference.vel");
    const auto program = parser.parse();
    require(program.labelIndex.contains("start"), "documented syntax lost the start label");

    bool foundInputOptions = false;
    bool foundScreen = false;
    for (const auto& statement : program.statements) {
        if (const auto* input = std::get_if<velora::script::InputCmd>(&statement.data))
            foundInputOptions = input->options.size() == 3;
        if (const auto* screen = std::get_if<velora::script::ScreenDef>(&statement.data))
            foundScreen = screen->name == "notice" && !screen->elements.empty();
    }
    require(foundInputOptions, "documented input options were not parsed");
    require(foundScreen, "documented screen was not parsed");
}

void testSaveRoundTrip(const fs::path& directory) {
    velora::core::GameStateManager manager(directory);
    velora::core::GameSnapshot snapshot;
    snapshot.scriptLine = 42;
    snapshot.currentLabel = "arrival";
    snapshot.backgroundPath = "backgrounds/station.png";
    snapshot.currentSpeaker = "Guide";
    snapshot.currentText = "Welcome.";
    snapshot.textComplete = true;
    snapshot.variables["name"] = std::string("Mira");
    snapshot.variables["visits"] = 3.0;
    snapshot.variables["ready"] = true;
    snapshot.sprites.push_back(
        {"guide", "sprites/guide.png", 0.5f, 0.5f, 0.8f, 1.1f, 1.1f, 3.0f, 2, true});
    manager.captureSnapshot(snapshot);
    require(manager.saveToSlot(1), "could not save slot");

    velora::core::GameStateManager loaded(directory);
    require(loaded.loadFromSlot(1), "could not load slot");
    const auto* restored = loaded.getCurrentSnapshot();
    require(restored != nullptr, "loaded snapshot is missing");
    require(restored->scriptLine == 42, "script position changed in save file");
    require(restored->currentLabel == "arrival", "label changed in save file");
    require(restored->sprites.size() == 1 && restored->sprites[0].flipped,
            "sprite state changed in save file");
    require(std::get<std::string>(restored->variables.at("name")) == "Mira",
            "string variable changed in save file");

    const auto slots = loaded.getSaveSlots();
    require(slots.at(1).occupied, "slot metadata did not detect the save");
    require(slots.at(1).preview == "Welcome.", "slot preview was not restored");
    require(!loaded.saveToSlot(-1), "negative save slot was accepted");
    require(!loaded.loadFromSlot(21), "out-of-range save slot was accepted");
}

void testCorruptSave(const fs::path& directory) {
    fs::create_directories(directory);
    std::ofstream output(directory / "save_2.vsav", std::ios::binary);
    output << "VSAV\x02\x00broken";
    output.close();
    velora::core::GameStateManager manager(directory);
    require(!manager.loadFromSlot(2), "corrupt save was accepted");
}

} // namespace

int main() {
    const auto unique = std::chrono::steady_clock::now().time_since_epoch().count();
    const fs::path directory =
        fs::temp_directory_path() / ("velora-core-tests-" + std::to_string(unique));
    try {
        testScriptPipeline();
        testBundledScript();
        testDocumentedSyntax();
        testSaveRoundTrip(directory);
        testCorruptSave(directory);
        std::error_code error;
        fs::remove_all(directory, error);
        std::cout << "All core tests passed.\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Test failure: " << error.what() << '\n';
        std::error_code cleanupError;
        fs::remove_all(directory, cleanupError);
        return 1;
    }
}
