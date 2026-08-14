#include "core/Engine.h"
#include "i18n/LocalizationManager.h"

#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>

#include <charconv>
#include <exception>
#include <iostream>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>

#ifdef _WIN32
#include <windows.h>
#endif

#ifndef VELORA_VERSION
#define VELORA_VERSION "0.2.0"
#endif

namespace {

struct LaunchConfig {
    std::string scriptPath = "assets/scripts/example.vel";
    bool debugMode = false;
    bool fullscreen = false;
    bool extractLocales = false;
    std::optional<int> width;
    std::optional<int> height;
};

void printHelp() {
    std::cout << "Velora Engine " << VELORA_VERSION << "\n\n"
              << "Usage: VeloraEngine [options]\n"
              << "  -s, --script <path>   VelScript entry file\n"
              << "  -d, --debug           Keep running after script parse errors\n"
              << "  -f, --fullscreen      Start in fullscreen mode\n"
              << "      --width <pixels>  Override saved window width\n"
              << "      --height <pixels> Override saved window height\n"
              << "      --extract-locales Generate localization templates\n"
              << "  -h, --help            Show this help\n"
              << "      --version         Print the version\n";
}

std::optional<int> parseDimension(std::string_view value) {
    int result = 0;
    const auto [end, error] = std::from_chars(value.data(), value.data() + value.size(), result);
    if (error != std::errc{} || end != value.data() + value.size() || result < 360 ||
        result > 7680) {
        return std::nullopt;
    }
    return result;
}

std::optional<LaunchConfig> parseArguments(int argc, char* argv[]) {
    LaunchConfig config;
    for (int index = 1; index < argc; ++index) {
        const std::string_view argument(argv[index]);
        if (argument == "-h" || argument == "--help") {
            printHelp();
            return std::nullopt;
        }
        if (argument == "--version") {
            std::cout << VELORA_VERSION << '\n';
            return std::nullopt;
        }
        if (argument == "-d" || argument == "--debug")
            config.debugMode = true;
        else if (argument == "-f" || argument == "--fullscreen")
            config.fullscreen = true;
        else if (argument == "--extract-locales")
            config.extractLocales = true;
        else if (argument == "-s" || argument == "--script") {
            if (++index >= argc)
                throw std::runtime_error("--script requires a path");
            config.scriptPath = argv[index];
        } else if (argument == "--width" || argument == "--height") {
            if (++index >= argc)
                throw std::runtime_error(std::string(argument) + " requires a value");
            const auto value = parseDimension(argv[index]);
            if (!value)
                throw std::runtime_error(std::string(argument) + " must be between 360 and 7680");
            if (argument == "--width")
                config.width = *value;
            else
                config.height = *value;
        } else {
            throw std::runtime_error("unknown option: " + std::string(argument));
        }
    }
    if (config.width.has_value() != config.height.has_value()) {
        throw std::runtime_error("--width and --height must be specified together");
    }
    return config;
}

int extractLocales() {
    using velora::i18n::LocalizationManager;
    const auto strings = LocalizationManager::extractFromScripts("assets/scripts");
    if (strings.empty())
        return 1;
    const bool templateWritten =
        LocalizationManager::generateTemplate(strings, "assets/locales/template.json");
    const bool mapWritten =
        LocalizationManager::generateKeyMap(strings, "assets/locales/key_map.json");
    return templateWritten && mapWritten ? 0 : 1;
}

} // namespace

int main(int argc, char* argv[]) {
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
#endif

    try {
        const auto launch = parseArguments(argc, argv);
        if (!launch)
            return 0;
        if (launch->extractLocales)
            return extractLocales();

        velora::core::EngineConfig config;
        config.windowTitle = "Velora Engine";
        config.fullscreen = launch->fullscreen;
        config.debugMode = launch->debugMode;
        config.scriptPath = launch->scriptPath;
        if (launch->width && launch->height) {
            config.windowWidth = *launch->width;
            config.windowHeight = *launch->height;
            config.overrideWindowSize = true;
        }

        velora::core::Engine engine;
        if (!engine.initialize(config)) {
            std::cerr << "Velora Engine could not initialize.\n";
            return 1;
        }
        engine.run();
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Velora Engine: " << error.what() << '\n';
        return 2;
    }
}
