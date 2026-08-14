#include "AssetManager.h"

#include <filesystem>
#include <fstream>
#include <iostream>

namespace fs = std::filesystem;

namespace velora::asset {

AssetManager::AssetManager(const std::string& assetRoot)
    : m_assetRoot(fs::absolute(assetRoot).lexically_normal().string()) {
    if (!fs::is_directory(m_assetRoot)) {
        std::cerr << "[assets] directory not found: " << m_assetRoot << '\n';
    }
}

bool AssetManager::isSafeRelativePath(const std::string& path) {
    const fs::path normalized = fs::path(path).lexically_normal();
    if (path.empty() || normalized.is_absolute())
        return false;
    for (const auto& part : normalized) {
        if (part == "..")
            return false;
    }
    return true;
}

std::string AssetManager::resolvePath(const std::string& logicalPath) const {
    if (!isSafeRelativePath(logicalPath))
        return {};

    for (const auto& [prefix, directory] : m_mountPoints) {
        if (!logicalPath.starts_with(prefix))
            continue;
        const auto relative = fs::path(logicalPath.substr(prefix.size())).lexically_normal();
        const auto candidate = fs::path(directory) / relative;
        if (fs::is_regular_file(candidate))
            return candidate.string();
    }

    const auto candidate = fs::path(m_assetRoot) / fs::path(logicalPath).lexically_normal();
    return fs::is_regular_file(candidate) ? candidate.string() : std::string{};
}

bool AssetManager::exists(const std::string& logicalPath) const {
    return !resolvePath(logicalPath).empty();
}

void AssetManager::mountDirectory(const std::string& prefix, const std::string& directory) {
    if (!isSafeRelativePath(prefix) || !fs::is_directory(directory)) {
        std::cerr << "[assets] invalid mount: " << prefix << " -> " << directory << '\n';
        return;
    }
    m_mountPoints[prefix] = fs::absolute(directory).lexically_normal().string();
}

std::vector<std::uint8_t> AssetManager::readFile(const std::string& logicalPath) const {
    const std::string path = resolvePath(logicalPath);
    if (path.empty())
        return {};

    std::ifstream file(path, std::ios::binary | std::ios::ate);
    if (!file)
        return {};
    const auto end = file.tellg();
    if (end < 0)
        return {};

    std::vector<std::uint8_t> data(static_cast<std::size_t>(end));
    file.seekg(0);
    file.read(reinterpret_cast<char*>(data.data()), static_cast<std::streamsize>(data.size()));
    return file ? data : std::vector<std::uint8_t>{};
}

std::string AssetManager::readTextFile(const std::string& logicalPath) const {
    const std::string path = resolvePath(logicalPath);
    if (path.empty())
        return {};

    std::ifstream file(path, std::ios::binary);
    if (!file)
        return {};
    return {std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
}

} // namespace velora::asset
