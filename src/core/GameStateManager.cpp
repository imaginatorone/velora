#include "GameStateManager.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <array>
#include <chrono>
#include <cstring>
#include <fstream>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <system_error>

namespace fs = std::filesystem;
using json = nlohmann::json;

namespace velora::core {
namespace {

constexpr uint16_t SAVE_VERSION = 2;
constexpr uint16_t PERSISTENT_VERSION = 2;
constexpr uintmax_t MAX_SAVE_BYTES = 16 * 1024 * 1024;
constexpr int MIN_SLOT = 0;
constexpr int MAX_SLOT = 20;

// Version 2 uses a little-endian header followed by a CBOR payload.

int64_t nowSeconds() {
    return std::chrono::duration_cast<std::chrono::seconds>(
               std::chrono::system_clock::now().time_since_epoch())
        .count();
}

bool validSlot(int slot) {
    return slot >= MIN_SLOT && slot <= MAX_SLOT;
}

json valueToJson(const VarValue& value) {
    return std::visit(
        [](const auto& item) -> json {
            using T = std::decay_t<decltype(item)>;
            if constexpr (std::is_same_v<T, std::string>)
                return {{"type", "string"}, {"value", item}};
            if constexpr (std::is_same_v<T, double>)
                return {{"type", "number"}, {"value", item}};
            return {{"type", "boolean"}, {"value", item}};
        },
        value);
}

VarValue valueFromJson(const json& data) {
    const std::string type = data.at("type").get<std::string>();
    if (type == "string")
        return data.at("value").get<std::string>();
    if (type == "number")
        return data.at("value").get<double>();
    if (type == "boolean")
        return data.at("value").get<bool>();
    throw std::runtime_error("unknown variable type");
}

json snapshotToJson(const GameSnapshot& snapshot) {
    json variables = json::object();
    for (const auto& [name, value] : snapshot.variables)
        variables[name] = valueToJson(value);

    json sprites = json::array();
    for (const auto& sprite : snapshot.sprites) {
        sprites.push_back({
            {"id", sprite.id},
            {"image", sprite.imagePath},
            {"x", sprite.x},
            {"y", sprite.y},
            {"alpha", sprite.alpha},
            {"scale_x", sprite.scaleX},
            {"scale_y", sprite.scaleY},
            {"rotation", sprite.rotation},
            {"z_order", sprite.zOrder},
            {"flipped", sprite.flipped},
        });
    }

    json audio = json::array();
    for (const auto& track : snapshot.audioTracks) {
        audio.push_back({
            {"channel", track.channel},
            {"file", track.filePath},
            {"position", track.position},
            {"volume", track.volume},
            {"playing", track.playing},
        });
    }

    return {
        {"id", snapshot.id},
        {"script_line", snapshot.scriptLine},
        {"label", snapshot.currentLabel},
        {"background", snapshot.backgroundPath},
        {"speaker", snapshot.currentSpeaker},
        {"text", snapshot.currentText},
        {"text_complete", snapshot.textComplete},
        {"timestamp", snapshot.timestamp},
        {"variables", std::move(variables)},
        {"sprites", std::move(sprites)},
        {"audio", std::move(audio)},
    };
}

GameSnapshot snapshotFromJson(const json& data) {
    GameSnapshot snapshot;
    snapshot.id = data.value("id", uint64_t{0});
    snapshot.scriptLine = data.value("script_line", size_t{0});
    snapshot.currentLabel = data.value("label", std::string{});
    snapshot.backgroundPath = data.value("background", std::string{});
    snapshot.currentSpeaker = data.value("speaker", std::string{});
    snapshot.currentText = data.value("text", std::string{});
    snapshot.textComplete = data.value("text_complete", false);
    snapshot.timestamp = data.value("timestamp", int64_t{0});

    const auto& variables = data.value("variables", json::object());
    if (!variables.is_object() || variables.size() > 10000)
        throw std::runtime_error("invalid variables");
    for (const auto& [name, value] : variables.items())
        snapshot.variables[name] = valueFromJson(value);

    const auto& sprites = data.value("sprites", json::array());
    if (!sprites.is_array() || sprites.size() > 1000)
        throw std::runtime_error("invalid sprites");
    for (const auto& item : sprites) {
        SpriteState sprite;
        sprite.id = item.value("id", std::string{});
        sprite.imagePath = item.value("image", std::string{});
        sprite.x = item.value("x", 0.5f);
        sprite.y = item.value("y", 0.5f);
        sprite.alpha = item.value("alpha", 1.0f);
        sprite.scaleX = item.value("scale_x", 1.0f);
        sprite.scaleY = item.value("scale_y", 1.0f);
        sprite.rotation = item.value("rotation", 0.0f);
        sprite.zOrder = item.value("z_order", 0);
        sprite.flipped = item.value("flipped", false);
        snapshot.sprites.push_back(std::move(sprite));
    }

    const auto& audio = data.value("audio", json::array());
    if (!audio.is_array() || audio.size() > 128)
        throw std::runtime_error("invalid audio state");
    for (const auto& item : audio) {
        AudioTrackState track;
        track.channel = item.value("channel", std::string{});
        track.filePath = item.value("file", std::string{});
        track.position = item.value("position", 0.0);
        track.volume = item.value("volume", 1.0f);
        track.playing = item.value("playing", false);
        snapshot.audioTracks.push_back(std::move(track));
    }
    return snapshot;
}

class LegacyReader {
  public:
    explicit LegacyReader(const std::vector<uint8_t>& bytes) : m_bytes(bytes) {}

    template <typename T> T number() {
        if (m_position + sizeof(T) > m_bytes.size())
            throw std::runtime_error("truncated legacy save");
        T value{};
        std::memcpy(&value, m_bytes.data() + m_position, sizeof(T));
        m_position += sizeof(T);
        return value;
    }

    std::string string() {
        const uint32_t length = number<uint32_t>();
        if (length > 4 * 1024 * 1024 || m_position + length > m_bytes.size()) {
            throw std::runtime_error("invalid legacy string");
        }
        std::string value(reinterpret_cast<const char*>(m_bytes.data() + m_position), length);
        m_position += length;
        return value;
    }

  private:
    const std::vector<uint8_t>& m_bytes;
    size_t m_position = 0;
};

GameSnapshot legacySnapshot(const std::vector<uint8_t>& bytes) {
    // Version 1 used the host byte order; keep this reader only for migration.
    LegacyReader reader(bytes);
    GameSnapshot snapshot;
    snapshot.id = reader.number<uint64_t>();
    snapshot.scriptLine = static_cast<size_t>(reader.number<uint64_t>());
    snapshot.currentLabel = reader.string();
    snapshot.backgroundPath = reader.string();
    snapshot.currentSpeaker = reader.string();
    snapshot.currentText = reader.string();

    const uint32_t variableCount = reader.number<uint32_t>();
    if (variableCount > 10000)
        throw std::runtime_error("too many legacy variables");
    for (uint32_t index = 0; index < variableCount; ++index) {
        const std::string name = reader.string();
        const uint8_t type = reader.number<uint8_t>();
        if (type == 0)
            snapshot.variables[name] = reader.string();
        else if (type == 1)
            snapshot.variables[name] = reader.number<double>();
        else if (type == 2)
            snapshot.variables[name] = reader.number<uint8_t>() != 0;
        else
            throw std::runtime_error("unknown legacy variable type");
    }

    const uint32_t spriteCount = reader.number<uint32_t>();
    if (spriteCount > 1000)
        throw std::runtime_error("too many legacy sprites");
    for (uint32_t index = 0; index < spriteCount; ++index) {
        SpriteState sprite;
        sprite.id = reader.string();
        sprite.imagePath = reader.string();
        sprite.x = reader.number<float>();
        sprite.y = reader.number<float>();
        sprite.alpha = reader.number<float>();
        sprite.scaleX = reader.number<float>();
        sprite.scaleY = reader.number<float>();
        snapshot.sprites.push_back(std::move(sprite));
    }
    return snapshot;
}

bool writeVersionedFile(const fs::path& path, std::string_view magic, uint16_t version,
                        const std::vector<uint8_t>& payload) {
    std::error_code error;
    fs::create_directories(path.parent_path(), error);
    if (error)
        return false;
    const fs::path temporary = path.string() + ".tmp";
    {
        std::ofstream output(temporary, std::ios::binary | std::ios::trunc);
        if (!output)
            return false;
        output.write(magic.data(), 4);
        const std::array<uint8_t, 2> versionBytes = {static_cast<uint8_t>(version & 0xff),
                                                     static_cast<uint8_t>((version >> 8) & 0xff)};
        output.write(reinterpret_cast<const char*>(versionBytes.data()), 2);
        output.write(reinterpret_cast<const char*>(payload.data()),
                     static_cast<std::streamsize>(payload.size()));
        if (!output)
            return false;
    }
    // Replace only after the temporary file has been written completely.
    fs::remove(path, error);
    error.clear();
    fs::rename(temporary, path, error);
    return !error;
}

} // namespace

GameStateManager::GameStateManager(fs::path saveDirectory) : m_saveDir(std::move(saveDirectory)) {
    m_history.reserve(m_maxHistory);
}

GameStateManager::~GameStateManager() = default;

void GameStateManager::captureSnapshot(const GameSnapshot& snapshot) {
    GameSnapshot copy = snapshot;
    copy.id = m_nextSnapId++;
    copy.timestamp = nowSeconds();
    if (m_historyPos >= 0 && m_historyPos < static_cast<int>(m_history.size()) - 1) {
        m_history.erase(m_history.begin() + m_historyPos + 1, m_history.end());
    }
    if (m_history.size() >= m_maxHistory)
        m_history.erase(m_history.begin());
    m_history.push_back(std::move(copy));
    m_historyPos = static_cast<int>(m_history.size()) - 1;
}

bool GameStateManager::rollback() {
    if (m_historyPos <= 0)
        return false;
    --m_historyPos;
    return true;
}

bool GameStateManager::rollForward() {
    if (m_historyPos < 0 || m_historyPos >= static_cast<int>(m_history.size()) - 1)
        return false;
    ++m_historyPos;
    return true;
}

const GameSnapshot* GameStateManager::getCurrentSnapshot() const {
    if (m_historyPos < 0 || m_historyPos >= static_cast<int>(m_history.size()))
        return nullptr;
    return &m_history[static_cast<size_t>(m_historyPos)];
}

bool GameStateManager::saveToSlot(int slot) {
    if (!validSlot(slot))
        return false;
    const GameSnapshot* snapshot = getCurrentSnapshot();
    if (!snapshot)
        return false;
    return writeVersionedFile(m_saveDir / ("save_" + std::to_string(slot) + ".vsav"), "VSAV",
                              SAVE_VERSION, serializeSnapshot(*snapshot));
}

bool GameStateManager::loadFromSlot(int slot) {
    if (!validSlot(slot))
        return false;
    auto snapshot = readSnapshotFile(m_saveDir / ("save_" + std::to_string(slot) + ".vsav"));
    if (!snapshot)
        return false;
    captureSnapshot(*snapshot);
    return true;
}

bool GameStateManager::quickSave() {
    return saveToSlot(0);
}
bool GameStateManager::quickLoad() {
    return loadFromSlot(0);
}

bool GameStateManager::deleteSave(int slot) {
    if (!validSlot(slot))
        return false;
    std::error_code error;
    return fs::remove(m_saveDir / ("save_" + std::to_string(slot) + ".vsav"), error) && !error;
}

std::vector<GameStateManager::SaveSlotInfo> GameStateManager::getSaveSlots() const {
    std::vector<SaveSlotInfo> slots;
    slots.reserve(MAX_SLOT + 1);
    for (int slot = MIN_SLOT; slot <= MAX_SLOT; ++slot) {
        SaveSlotInfo info{slot, false, 0, {}, {}};
        if (auto snapshot =
                readSnapshotFile(m_saveDir / ("save_" + std::to_string(slot) + ".vsav"))) {
            info.occupied = true;
            info.timestamp = snapshot->timestamp;
            info.label = snapshot->currentLabel;
            info.preview = snapshot->currentText;
        }
        slots.push_back(std::move(info));
    }
    return slots;
}

std::vector<uint8_t> GameStateManager::serializeSnapshot(const GameSnapshot& snapshot) {
    return json::to_cbor(snapshotToJson(snapshot));
}

GameSnapshot GameStateManager::deserializeSnapshot(const std::vector<uint8_t>& data) {
    return snapshotFromJson(json::from_cbor(data));
}

std::optional<GameSnapshot> GameStateManager::readSnapshotFile(const fs::path& path) {
    try {
        std::error_code error;
        if (!fs::is_regular_file(path, error) || fs::file_size(path, error) > MAX_SAVE_BYTES)
            return std::nullopt;
        std::ifstream input(path, std::ios::binary);
        char magic[4]{};
        std::array<uint8_t, 2> versionBytes{};
        input.read(magic, 4);
        input.read(reinterpret_cast<char*>(versionBytes.data()), 2);
        if (!input || std::memcmp(magic, "VSAV", 4) != 0)
            return std::nullopt;
        const uint16_t version =
            static_cast<uint16_t>(versionBytes[0]) | (static_cast<uint16_t>(versionBytes[1]) << 8);
        std::vector<uint8_t> payload(std::istreambuf_iterator<char>(input), {});
        if (version == SAVE_VERSION)
            return deserializeSnapshot(payload);
        if (version == 1)
            return legacySnapshot(payload);
    } catch (const std::exception& error) {
        std::cerr << "[StateManager] Ignoring invalid save " << path << ": " << error.what()
                  << '\n';
    }
    return std::nullopt;
}

void GameStateManager::setPersistent(const std::string& key, const VarValue& value) {
    m_persistentVars[key] = value;
    savePersistent();
}

std::optional<VarValue> GameStateManager::getPersistent(const std::string& key) const {
    const auto found = m_persistentVars.find(key);
    if (found == m_persistentVars.end())
        return std::nullopt;
    return found->second;
}

bool GameStateManager::loadPersistent() {
    try {
        const fs::path path = m_saveDir / "persistent.vsav";
        std::error_code error;
        if (!fs::exists(path, error))
            return true;
        if (fs::file_size(path, error) > MAX_SAVE_BYTES)
            return false;
        std::ifstream input(path, std::ios::binary);
        char magic[4]{};
        std::array<uint8_t, 2> versionBytes{};
        input.read(magic, 4);
        input.read(reinterpret_cast<char*>(versionBytes.data()), 2);
        const uint16_t version =
            static_cast<uint16_t>(versionBytes[0]) | (static_cast<uint16_t>(versionBytes[1]) << 8);
        if (!input || std::memcmp(magic, "VPER", 4) != 0 || version != PERSISTENT_VERSION)
            return false;
        std::vector<uint8_t> payload(std::istreambuf_iterator<char>(input), {});
        const json data = json::from_cbor(payload);
        if (!data.is_object() || data.size() > 10000)
            return false;
        m_persistentVars.clear();
        for (const auto& [key, value] : data.items())
            m_persistentVars[key] = valueFromJson(value);
        return true;
    } catch (...) {
        return false;
    }
}

bool GameStateManager::savePersistent() {
    json data = json::object();
    for (const auto& [key, value] : m_persistentVars)
        data[key] = valueToJson(value);
    return writeVersionedFile(m_saveDir / "persistent.vsav", "VPER", PERSISTENT_VERSION,
                              json::to_cbor(data));
}

void GameStateManager::addToBacklog(const std::string& speaker, const std::string& text) {
    m_backlog.push_back({speaker, text, nowSeconds()});
    if (m_backlog.size() > MAX_BACKLOG)
        m_backlog.erase(m_backlog.begin());
}

} // namespace velora::core
