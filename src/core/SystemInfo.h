#pragma once

#include <ctime>
#include <optional>
#include <string>
#include <string_view>

namespace velora::core::system {

[[nodiscard]] std::string userName();
[[nodiscard]] std::string preferredLocale();
[[nodiscard]] std::optional<std::string> formatLocalTime(std::time_t time,
                                                         std::string_view format);

} // namespace velora::core::system
