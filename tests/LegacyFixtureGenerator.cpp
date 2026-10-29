#include "core/GameStateManager.h"

#include <bit>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <string_view>
#include <type_traits>
#include <variant>
#include <vector>

namespace fs = std::filesystem;

namespace {

constexpr std::string_view CPP_V2_DIRECTORY = "cpp-v2";
constexpr std::string_view SYNTHETIC_V1_DIRECTORY = "synthetic-v1-host-native";
constexpr int FIXTURE_SLOT = 1;

constexpr size_t V2_SCRIPT_LINE = 424242;
constexpr std::string_view V2_LABEL = "маршрут/arrival-🌙";
constexpr std::string_view V2_BACKGROUND = "backgrounds/ночной-вокзал.png";
constexpr std::string_view V2_SPEAKER = "";
constexpr std::string_view V2_TEXT = "Привет, 世界 — the fixture is alive. 🌙";

constexpr uint64_t V1_STORED_ID = UINT64_C(0x1122334455667788);
constexpr uint64_t V1_SCRIPT_LINE = 91;
constexpr std::string_view V1_LABEL = "synthetic-v1";
constexpr std::string_view V1_BACKGROUND = "";
constexpr std::string_view V1_SPEAKER = "Legacy Oracle";
constexpr std::string_view V1_TEXT = "Synthetic v1 fixture — не authentic user data";

void require(bool condition, std::string_view message) {
    if (!condition)
        throw std::runtime_error(std::string(message));
}

bool closeEnough(double actual, double expected, double tolerance = 0.000001) {
    return std::abs(actual - expected) <= tolerance;
}

template <typename T>
const T& requireValue(const velora::core::VarValue& value, std::string_view message) {
    const auto* typed = std::get_if<T>(&value);
    require(typed != nullptr, message);
    return *typed;
}

velora::core::GameSnapshot makeV2Snapshot() {
    velora::core::GameSnapshot snapshot;
    // captureSnapshot intentionally replaces both id and timestamp once. This exercises the
    // production capture path instead of manufacturing a CBOR payload in the fixture tool.
    snapshot.id = 999;
    snapshot.scriptLine = V2_SCRIPT_LINE;
    snapshot.currentLabel = V2_LABEL;
    snapshot.backgroundPath = V2_BACKGROUND;
    snapshot.currentSpeaker = V2_SPEAKER;
    snapshot.currentText = V2_TEXT;
    snapshot.textComplete = true;

    snapshot.variables["empty_string"] = std::string{};
    snapshot.variables["unicode_string"] = std::string("Значение / 値 / 🌙");
    snapshot.variables["number"] = -12345.625;
    snapshot.variables["boolean"] = true;

    snapshot.sprites.push_back(
        {"hero", "sprites/героиня.png", 0.125f, 0.875f, 0.25f, 0.75f, 1.25f, -15.5f, -3, false});
    snapshot.sprites.push_back(
        {"rival", "sprites/rival.png", -0.5f, 1.5f, 0.875f, 2.0f, 0.5f, 270.0f, 42, true});

    snapshot.audioTracks.push_back({"bgm", "audio/ночная-тема.ogg", 12.5, 0.75f, true});
    snapshot.audioTracks.push_back({"sfx", "", 0.0, 0.0f, false});
    snapshot.audioTracks.push_back({"voice", "audio/voice/line-001.ogg", 123.456, 1.0f, true});
    return snapshot;
}

template <typename T> void appendHostNumber(std::vector<uint8_t>& bytes, T value) {
    static_assert(std::is_trivially_copyable_v<T>);
    const size_t offset = bytes.size();
    bytes.resize(offset + sizeof(T));
    std::memcpy(bytes.data() + offset, &value, sizeof(T));
}

void appendHostString(std::vector<uint8_t>& bytes, std::string_view value) {
    require(value.size() <= std::numeric_limits<uint32_t>::max(),
            "synthetic v1 string is too large");
    appendHostNumber(bytes, static_cast<uint32_t>(value.size()));
    bytes.insert(bytes.end(), value.begin(), value.end());
}

void writeSyntheticV1(const fs::path& path) {
    std::vector<uint8_t> bytes{'V', 'S', 'A', 'V', 0x01, 0x00};
    appendHostNumber(bytes, V1_STORED_ID);
    appendHostNumber(bytes, V1_SCRIPT_LINE);
    appendHostString(bytes, V1_LABEL);
    appendHostString(bytes, V1_BACKGROUND);
    appendHostString(bytes, V1_SPEAKER);
    appendHostString(bytes, V1_TEXT);

    appendHostNumber(bytes, uint32_t{3});

    appendHostString(bytes, "legacy.string");
    appendHostNumber(bytes, uint8_t{0});
    appendHostString(bytes, "строка v1");

    appendHostString(bytes, "legacy.number");
    appendHostNumber(bytes, uint8_t{1});
    appendHostNumber(bytes, 17.25);

    appendHostString(bytes, "legacy.boolean");
    appendHostNumber(bytes, uint8_t{2});
    appendHostNumber(bytes, uint8_t{1});

    appendHostNumber(bytes, uint32_t{1});
    appendHostString(bytes, "legacy-hero");
    appendHostString(bytes, "sprites/legacy.png");
    appendHostNumber(bytes, -0.25f);
    appendHostNumber(bytes, 1.25f);
    appendHostNumber(bytes, 0.625f);
    appendHostNumber(bytes, 1.5f);
    appendHostNumber(bytes, 0.5f);

    std::error_code error;
    fs::create_directories(path.parent_path(), error);
    require(!error, "could not create the synthetic v1 fixture directory");

    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    require(static_cast<bool>(output), "could not open the synthetic v1 fixture");
    output.write(reinterpret_cast<const char*>(bytes.data()),
                 static_cast<std::streamsize>(bytes.size()));
    require(static_cast<bool>(output), "could not write the synthetic v1 fixture");
}

void generateFixtures(const fs::path& outputDirectory) {
    require(!outputDirectory.empty(), "an explicit output directory is required");

    const fs::path v2Directory = outputDirectory / CPP_V2_DIRECTORY;
    std::error_code error;
    fs::create_directories(v2Directory, error);
    require(!error, "could not create the C++ v2 fixture directory");

    velora::core::GameStateManager manager(v2Directory);
    manager.captureSnapshot(makeV2Snapshot());
    require(manager.saveToSlot(FIXTURE_SLOT), "the C++ oracle could not write VSAV v2");

    manager.setPersistent("persistent.empty", std::string{});
    manager.setPersistent("persistent.unicode", std::string("Сохранено / 保存 / ✓"));
    manager.setPersistent("persistent.number", -42.125);
    manager.setPersistent("persistent.boolean", false);
    require(manager.savePersistent(), "the C++ oracle could not write VPER v2");

    writeSyntheticV1(outputDirectory / SYNTHETIC_V1_DIRECTORY / "save_1.vsav");
}

void verifyV2Snapshot(const fs::path& fixtureDirectory) {
    velora::core::GameStateManager manager(fixtureDirectory);
    const auto slots = manager.getSaveSlots();
    require(slots.at(FIXTURE_SLOT).occupied, "VSAV v2 fixture was not recognized");
    require(slots.at(FIXTURE_SLOT).timestamp > 0, "VSAV v2 capture timestamp is missing");
    require(slots.at(FIXTURE_SLOT).label == V2_LABEL, "VSAV v2 slot label changed");
    require(slots.at(FIXTURE_SLOT).preview == V2_TEXT, "VSAV v2 slot preview changed");

    require(manager.loadFromSlot(FIXTURE_SLOT), "the C++ oracle could not read VSAV v2");
    const auto* snapshot = manager.getCurrentSnapshot();
    require(snapshot != nullptr, "the C++ oracle returned no VSAV v2 snapshot");
    require(snapshot->id == 0, "VSAV v2 loaded snapshot id changed");
    require(snapshot->scriptLine == V2_SCRIPT_LINE, "VSAV v2 script line changed");
    require(snapshot->currentLabel == V2_LABEL, "VSAV v2 label changed");
    require(snapshot->backgroundPath == V2_BACKGROUND, "VSAV v2 background changed");
    require(snapshot->currentSpeaker == V2_SPEAKER, "VSAV v2 empty speaker changed");
    require(snapshot->currentText == V2_TEXT, "VSAV v2 Unicode text changed");
    require(snapshot->textComplete, "VSAV v2 text-complete flag changed");
    require(snapshot->timestamp > 0, "VSAV v2 loaded capture timestamp is missing");

    require(snapshot->variables.size() == 4, "VSAV v2 variable count changed");
    require(requireValue<std::string>(snapshot->variables.at("empty_string"),
                                      "VSAV v2 empty string type changed")
                .empty(),
            "VSAV v2 empty string value changed");
    require(requireValue<std::string>(snapshot->variables.at("unicode_string"),
                                      "VSAV v2 Unicode string type changed") ==
                "Значение / 値 / 🌙",
            "VSAV v2 Unicode string value changed");
    require(closeEnough(requireValue<double>(snapshot->variables.at("number"),
                                             "VSAV v2 number type changed"),
                        -12345.625),
            "VSAV v2 number value changed");
    require(requireValue<bool>(snapshot->variables.at("boolean"), "VSAV v2 boolean type changed"),
            "VSAV v2 boolean value changed");

    require(snapshot->sprites.size() == 2, "VSAV v2 sprite count changed");
    const auto& hero = snapshot->sprites.at(0);
    require(hero.id == "hero" && hero.imagePath == "sprites/героиня.png",
            "VSAV v2 first sprite identity changed");
    require(closeEnough(hero.x, 0.125) && closeEnough(hero.y, 0.875) &&
                closeEnough(hero.alpha, 0.25) && closeEnough(hero.scaleX, 0.75) &&
                closeEnough(hero.scaleY, 1.25) && closeEnough(hero.rotation, -15.5) &&
                hero.zOrder == -3 && !hero.flipped,
            "VSAV v2 first sprite values changed");

    const auto& rival = snapshot->sprites.at(1);
    require(rival.id == "rival" && rival.imagePath == "sprites/rival.png",
            "VSAV v2 second sprite identity changed");
    require(closeEnough(rival.x, -0.5) && closeEnough(rival.y, 1.5) &&
                closeEnough(rival.alpha, 0.875) && closeEnough(rival.scaleX, 2.0) &&
                closeEnough(rival.scaleY, 0.5) && closeEnough(rival.rotation, 270.0) &&
                rival.zOrder == 42 && rival.flipped,
            "VSAV v2 second sprite values changed");

    require(snapshot->audioTracks.size() == 3, "VSAV v2 audio-track count changed");
    const auto& bgm = snapshot->audioTracks.at(0);
    require(bgm.channel == "bgm" && bgm.filePath == "audio/ночная-тема.ogg" &&
                closeEnough(bgm.position, 12.5) && closeEnough(bgm.volume, 0.75) && bgm.playing,
            "VSAV v2 BGM values changed");
    const auto& sfx = snapshot->audioTracks.at(1);
    require(sfx.channel == "sfx" && sfx.filePath.empty() && closeEnough(sfx.position, 0.0) &&
                closeEnough(sfx.volume, 0.0) && !sfx.playing,
            "VSAV v2 SFX values changed");
    const auto& voice = snapshot->audioTracks.at(2);
    require(voice.channel == "voice" && voice.filePath == "audio/voice/line-001.ogg" &&
                closeEnough(voice.position, 123.456) && closeEnough(voice.volume, 1.0) &&
                voice.playing,
            "VSAV v2 voice values changed");
}

void verifyV2Persistent(const fs::path& fixtureDirectory) {
    velora::core::GameStateManager manager(fixtureDirectory);
    require(manager.loadPersistent(), "the C++ oracle could not read VPER v2");

    const auto empty = manager.getPersistent("persistent.empty");
    require(empty.has_value(), "VPER v2 empty string is missing");
    require(requireValue<std::string>(*empty, "VPER v2 empty string type changed").empty(),
            "VPER v2 empty string value changed");

    const auto unicode = manager.getPersistent("persistent.unicode");
    require(unicode.has_value(), "VPER v2 Unicode string is missing");
    require(requireValue<std::string>(*unicode, "VPER v2 Unicode string type changed") ==
                "Сохранено / 保存 / ✓",
            "VPER v2 Unicode string value changed");

    const auto number = manager.getPersistent("persistent.number");
    require(number.has_value(), "VPER v2 number is missing");
    require(closeEnough(requireValue<double>(*number, "VPER v2 number type changed"), -42.125),
            "VPER v2 number value changed");

    const auto boolean = manager.getPersistent("persistent.boolean");
    require(boolean.has_value(), "VPER v2 boolean is missing");
    require(!requireValue<bool>(*boolean, "VPER v2 boolean type changed"),
            "VPER v2 boolean value changed");
}

void verifySyntheticV1(const fs::path& fixtureDirectory) {
    velora::core::GameStateManager manager(fixtureDirectory);
    const auto slots = manager.getSaveSlots();
    require(slots.at(FIXTURE_SLOT).occupied, "synthetic VSAV v1 fixture was not recognized");
    require(slots.at(FIXTURE_SLOT).timestamp == 0,
            "synthetic VSAV v1 unexpectedly acquired a stored timestamp");
    require(slots.at(FIXTURE_SLOT).label == V1_LABEL, "synthetic VSAV v1 label changed");
    require(slots.at(FIXTURE_SLOT).preview == V1_TEXT, "synthetic VSAV v1 preview changed");

    require(manager.loadFromSlot(FIXTURE_SLOT), "the C++ oracle could not read synthetic VSAV v1");
    const auto* snapshot = manager.getCurrentSnapshot();
    require(snapshot != nullptr, "the C++ oracle returned no synthetic VSAV v1 snapshot");
    require(snapshot->scriptLine == V1_SCRIPT_LINE, "synthetic VSAV v1 script line changed");
    require(snapshot->currentLabel == V1_LABEL, "synthetic VSAV v1 label changed");
    require(snapshot->backgroundPath == V1_BACKGROUND,
            "synthetic VSAV v1 empty background changed");
    require(snapshot->currentSpeaker == V1_SPEAKER, "synthetic VSAV v1 speaker changed");
    require(snapshot->currentText == V1_TEXT, "synthetic VSAV v1 text changed");
    require(!snapshot->textComplete, "synthetic VSAV v1 gained a text-complete flag");

    require(snapshot->variables.size() == 3, "synthetic VSAV v1 variable count changed");
    require(requireValue<std::string>(snapshot->variables.at("legacy.string"),
                                      "synthetic VSAV v1 string type changed") == "строка v1",
            "synthetic VSAV v1 string value changed");
    require(closeEnough(requireValue<double>(snapshot->variables.at("legacy.number"),
                                             "synthetic VSAV v1 number type changed"),
                        17.25),
            "synthetic VSAV v1 number value changed");
    require(requireValue<bool>(snapshot->variables.at("legacy.boolean"),
                               "synthetic VSAV v1 boolean type changed"),
            "synthetic VSAV v1 boolean value changed");

    require(snapshot->sprites.size() == 1, "synthetic VSAV v1 sprite count changed");
    const auto& sprite = snapshot->sprites.front();
    require(sprite.id == "legacy-hero" && sprite.imagePath == "sprites/legacy.png" &&
                closeEnough(sprite.x, -0.25) && closeEnough(sprite.y, 1.25) &&
                closeEnough(sprite.alpha, 0.625) && closeEnough(sprite.scaleX, 1.5) &&
                closeEnough(sprite.scaleY, 0.5),
            "synthetic VSAV v1 sprite values changed");
    require(closeEnough(sprite.rotation, 0.0) && sprite.zOrder == 0 && !sprite.flipped,
            "synthetic VSAV v1 defaults changed");
    require(snapshot->audioTracks.empty(), "synthetic VSAV v1 unexpectedly gained audio state");
}

void verifyFixtures(const fs::path& fixtureDirectory) {
    require(!fixtureDirectory.empty(), "an explicit fixture directory is required");
    require(std::endian::native == std::endian::little,
            "the committed host-native v1 fixture requires a little-endian host");
    verifyV2Snapshot(fixtureDirectory / CPP_V2_DIRECTORY);
    verifyV2Persistent(fixtureDirectory / CPP_V2_DIRECTORY);
    verifySyntheticV1(fixtureDirectory / SYNTHETIC_V1_DIRECTORY);
}

void printUsage(const char* executable) {
    std::cerr << "Usage: " << executable
              << " (--generate <output-directory> | --verify <fixture-directory>)\n";
}

} // namespace

int main(int argc, char** argv) {
    if (argc != 3) {
        printUsage(argv[0]);
        return 2;
    }

    try {
        const fs::path directory = argv[2];
        if (std::string_view(argv[1]) == "--generate") {
            generateFixtures(directory);
            verifyFixtures(directory);
            std::cout << "Generated and verified legacy persistence fixtures in " << directory
                      << '\n';
            return 0;
        }
        if (std::string_view(argv[1]) == "--verify") {
            verifyFixtures(directory);
            std::cout << "Legacy persistence fixtures passed C++ oracle verification.\n";
            return 0;
        }
        printUsage(argv[0]);
        return 2;
    } catch (const std::exception& error) {
        std::cerr << "Legacy fixture failure: " << error.what() << '\n';
        return 1;
    }
}
