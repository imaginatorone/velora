using Velora.Platform;
using Velora.Runtime.Contracts;

namespace Velora.Persistence.Tests;

public sealed class SaveRepositoryTests
{
    [Fact]
    public void QuickSaveUsesLegacyManualSlotZero()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);

        repository.Save(SaveTarget.Quick, Snapshot(4, "quick"));

        SaveLoadResult manual = repository.Load(SaveTarget.Manual(0));
        Assert.True(manual.IsSuccess);
        Assert.Equal("quick", manual.Snapshot?.CurrentText);
        Assert.Contains("save_0.vsav", storage.Paths);
    }

    [Fact]
    public void AutosavesRotateSeparatelyAtConfiguredDepth()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);

        repository.Save(SaveTarget.Manual(1), Snapshot(1, "manual"));
        for (ulong id = 1; id <= 4; id++)
        {
            repository.Save(SaveTarget.Auto, Snapshot(id, $"auto-{id}"));
        }

        Assert.Equal("auto-4", repository.LoadAutosave(0).Snapshot?.CurrentText);
        Assert.Equal("auto-3", repository.LoadAutosave(1).Snapshot?.CurrentText);
        Assert.Equal("auto-2", repository.LoadAutosave(2).Snapshot?.CurrentText);
        Assert.Equal("manual", repository.Load(SaveTarget.Manual(1)).Snapshot?.CurrentText);
        Assert.DoesNotContain("autosaves/save_3.vsav", storage.Paths);
    }

    [Fact]
    public void LoadReportsMissingAndCorruptFilesWithoutThrowing()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);

        Assert.Equal(SaveLoadStatus.NotFound, repository.Load(SaveTarget.Manual(2)).Status);
        storage.WriteAtomic(StoragePath.Parse("save_2.vsav"), "not a save"u8);

        SaveLoadResult corrupt = repository.Load(SaveTarget.Manual(2));
        Assert.Equal(SaveLoadStatus.Invalid, corrupt.Status);
        Assert.NotEmpty(corrupt.Error ?? string.Empty);
    }

    [Fact]
    public void ListsAllLegacyManualSlotsWithPreviewMetadata()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);
        repository.Save(
            SaveTarget.Manual(20),
            Snapshot(20, "preview") with { CurrentSpeaker = "Mira", Timestamp = 123 });

        IReadOnlyList<ManualSaveInfo> slots = repository.ListManualSlots();

        Assert.Equal(21, slots.Count);
        Assert.Equal(SaveLoadStatus.NotFound, slots[0].Status);
        Assert.Equal(SaveLoadStatus.Success, slots[20].Status);
        Assert.Equal("Mira", slots[20].Speaker);
        Assert.Equal("preview", slots[20].Preview);
        Assert.Equal(123, slots[20].Timestamp);
    }

    [Fact]
    public void PersistentVariablesRoundTripAndMissingStoreIsEmpty()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);
        Assert.Empty(repository.LoadPersistent());

        repository.SavePersistent(
            new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
            {
                ["route"] = new RuntimeValue.StringValue("north"),
                ["seen"] = new RuntimeValue.BooleanValue(true),
            });

        IReadOnlyDictionary<string, RuntimeValue> restored = repository.LoadPersistent();
        Assert.Equal(new RuntimeValue.StringValue("north"), restored["route"]);
        Assert.Equal(new RuntimeValue.BooleanValue(true), restored["seen"]);
    }

    [Fact]
    public void RejectsInvalidTargetsSlotsAndAutosaveDepth()
    {
        var storage = new MemorySaveStorage();
        var repository = new SaveRepository(storage);

        Assert.Throws<ArgumentOutOfRangeException>(() => SaveTarget.Manual(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveTarget.Manual(21));
        Assert.Throws<ArgumentException>(() => repository.Load(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveRepository(storage, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => repository.LoadAutosave(3));
    }

    private static RuntimeSnapshot Snapshot(ulong id, string text) => new()
    {
        Id = id,
        ScriptLine = id,
        CurrentText = text,
    };

    private sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<StoragePath, byte[]> _files = [];

        public IEnumerable<string> Paths => _files.Keys.Select(static path => path.Value);

        public bool Exists(StoragePath path) => _files.ContainsKey(path);

        public bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data)
        {
            if (_files.TryGetValue(path, out byte[]? bytes))
            {
                data = bytes;
                return true;
            }

            data = default;
            return false;
        }

        public void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data)
        {
            _files[path] = data.ToArray();
        }

        public bool Delete(StoragePath path) => _files.Remove(path);

        public IReadOnlyList<StorageEntry> List() =>
            _files
                .OrderBy(static pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(static pair => new StorageEntry(pair.Key, pair.Value.Length, null))
                .ToArray();
    }
}
