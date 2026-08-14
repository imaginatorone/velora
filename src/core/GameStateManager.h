
#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <variant>
#include <optional>
#include <cstdint>
#include <filesystem>

namespace velora::core {

struct SpriteState {
    std::string id;
    std::string imagePath;
    float x = 0.5f, y = 0.5f;
    float alpha = 1.0f;
    float scaleX = 1.0f, scaleY = 1.0f;
    float rotation = 0.0f;
    int zOrder = 0;
    bool flipped = false;
};

struct AudioTrackState {
    std::string channel;
    std::string filePath;
    double position = 0.0;
    float volume = 1.0f;
    bool playing = false;
};

using VarValue = std::variant<std::string, double, bool>;

struct GameSnapshot {
    uint64_t id = 0;
    size_t scriptLine = 0;
    std::string currentLabel;

    std::unordered_map<std::string, VarValue> variables;

    std::string backgroundPath;
    std::vector<SpriteState> sprites;

    std::vector<AudioTrackState> audioTracks;

    std::string currentSpeaker;
    std::string currentText;
    bool textComplete = false;

    int64_t timestamp = 0;
};

class GameStateManager {
  public:
    explicit GameStateManager(std::filesystem::path saveDirectory = "saves");
    ~GameStateManager();

    void captureSnapshot(const GameSnapshot& snap);

    bool rollback();

    bool rollForward();

    [[nodiscard]] const GameSnapshot* getCurrentSnapshot() const;

    [[nodiscard]] size_t getHistoryDepth() const { return m_history.size(); }

    bool saveToSlot(int slot);

    bool loadFromSlot(int slot);

    bool quickSave();

    bool quickLoad();

    struct SaveSlotInfo {
        int slot;
        bool occupied;
        int64_t timestamp;
        std::string label;
        std::string preview;
    };
    [[nodiscard]] std::vector<SaveSlotInfo> getSaveSlots() const;

    void setSaveDirectory(const std::string& dir) { m_saveDir = dir; }

    bool deleteSave(int slot);

  private:
    static std::vector<uint8_t> serializeSnapshot(const GameSnapshot& snap);
    static GameSnapshot deserializeSnapshot(const std::vector<uint8_t>& data);
    static std::optional<GameSnapshot> readSnapshotFile(const std::filesystem::path& path);

    std::vector<GameSnapshot> m_history;
    int m_historyPos = -1;
    size_t m_maxHistory = 200;
    uint64_t m_nextSnapId = 0;

    std::filesystem::path m_saveDir = "saves";

  public:
    void setPersistent(const std::string& key, const VarValue& val);
    [[nodiscard]] std::optional<VarValue> getPersistent(const std::string& key) const;

    bool loadPersistent();
    bool savePersistent();

    static bool isPersistentVar(const std::string& name) { return name.starts_with("persistent."); }

  private:
    std::unordered_map<std::string, VarValue> m_persistentVars;

    struct BacklogEntry {
        std::string speaker;
        std::string text;
        int64_t timestamp;
    };

    void addToBacklog(const std::string& speaker, const std::string& text);

    [[nodiscard]] const std::vector<BacklogEntry>& getBacklog() const { return m_backlog; }

    void clearBacklog() { m_backlog.clear(); }

  private:
    std::vector<BacklogEntry> m_backlog;
    static constexpr size_t MAX_BACKLOG = 500;
};

} // namespace velora::core
