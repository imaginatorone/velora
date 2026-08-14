#include "Paths.h"

#include <SDL3/SDL.h>

#include <system_error>

namespace fs = std::filesystem;

namespace velora::core::paths {

fs::path executableDirectory() {
    if (const char* raw = SDL_GetBasePath())
        return fs::path(raw);
    return fs::current_path();
}

fs::path resourcePath(const fs::path& relativePath) {
    if (relativePath.is_absolute() && fs::exists(relativePath)) {
        return relativePath;
    }

    if (fs::exists(relativePath)) {
        return fs::absolute(relativePath);
    }

    const fs::path base = executableDirectory();
    const fs::path besideExecutable = base / relativePath;
    if (fs::exists(besideExecutable)) {
        return besideExecutable;
    }

#ifdef __APPLE__
    const fs::path bundleResource = base.parent_path().parent_path() / "Resources" / relativePath;
    if (fs::exists(bundleResource)) {
        return bundleResource;
    }
#endif

    return besideExecutable;
}

fs::path userDataDirectory() {
    if (char* raw = SDL_GetPrefPath("Velora", "Velora Engine")) {
        fs::path result(raw);
        SDL_free(raw);
        return result;
    }

    const fs::path fallback = fs::current_path() / ".velora";
    std::error_code error;
    fs::create_directories(fallback, error);
    return fallback;
}

std::optional<fs::path> scriptDataPath(std::string_view relativePath, bool createParent) {
    // Script I/O is confined to the per-user script-data directory.
    fs::path relative(relativePath);
    relative = relative.lexically_normal();
    if (relative.empty() || relative.is_absolute()) {
        return std::nullopt;
    }
    for (const auto& component : relative) {
        if (component == "..") {
            return std::nullopt;
        }
    }

    fs::path result = userDataDirectory() / "script-data" / relative;
    if (createParent) {
        std::error_code error;
        fs::create_directories(result.parent_path(), error);
        if (error) {
            return std::nullopt;
        }
    }
    return result;
}

} // namespace velora::core::paths
