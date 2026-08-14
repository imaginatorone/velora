#pragma once

#include <filesystem>
#include <optional>
#include <string_view>

namespace velora::core::paths {

[[nodiscard]] std::filesystem::path executableDirectory();
[[nodiscard]] std::filesystem::path resourcePath(const std::filesystem::path& relativePath);
[[nodiscard]] std::filesystem::path userDataDirectory();
[[nodiscard]] std::optional<std::filesystem::path> scriptDataPath(std::string_view relativePath,
                                                                  bool createParent = false);

} // namespace velora::core::paths
