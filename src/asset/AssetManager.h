#pragma once

#include <cstdint>
#include <string>
#include <unordered_map>
#include <vector>

namespace velora::asset {

class AssetManager {
  public:
    explicit AssetManager(const std::string& assetRoot);

    [[nodiscard]] std::string resolvePath(const std::string& logicalPath) const;
    [[nodiscard]] bool exists(const std::string& logicalPath) const;

    void mountDirectory(const std::string& prefix, const std::string& directory);

    [[nodiscard]] std::vector<std::uint8_t> readFile(const std::string& logicalPath) const;
    [[nodiscard]] std::string readTextFile(const std::string& logicalPath) const;
    [[nodiscard]] const std::string& getAssetRoot() const { return m_assetRoot; }

  private:
    [[nodiscard]] static bool isSafeRelativePath(const std::string& path);

    std::string m_assetRoot;
    std::unordered_map<std::string, std::string> m_mountPoints;
};

} // namespace velora::asset
