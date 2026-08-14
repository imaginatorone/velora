#include "SystemInfo.h"

#include <SDL3/SDL.h>

#include <array>

namespace velora::core::system {

std::string userName() {
    SDL_Environment* environment = SDL_GetEnvironment();
    if (environment) {
        for (const char* variable : {"USERNAME", "USER"}) {
            if (const char* value = SDL_GetEnvironmentVariable(environment, variable);
                value && *value) {
                return value;
            }
        }
    }
    return "Player";
}

std::string preferredLocale() {
    SDL_Locale** locales = SDL_GetPreferredLocales(nullptr);
    if (!locales || !locales[0]) {
        SDL_free(locales);
        return "en";
    }

    const SDL_Locale& preferred = *locales[0];
    std::string result = preferred.language;
    if (preferred.country) {
        result += '_';
        result += preferred.country;
    }
    SDL_free(locales);
    return result;
}

std::optional<std::string> formatLocalTime(std::time_t time, std::string_view format) {
    std::tm local{};
#ifdef _WIN32
    if (localtime_s(&local, &time) != 0) {
        return std::nullopt;
    }
#else
    if (!localtime_r(&time, &local)) {
        return std::nullopt;
    }
#endif

    std::array<char, 64> buffer{};
    if (std::strftime(buffer.data(), buffer.size(), std::string(format).c_str(), &local) == 0) {
        return std::nullopt;
    }
    return std::string(buffer.data());
}

} // namespace velora::core::system
