using Velora.Platform.Desktop.Tests.TestSupport;
using Velora.Persistence;
using Velora.Runtime.Contracts;

namespace Velora.Platform.Desktop.Tests;

public sealed class DesktopSaveStorageTests
{
    [Fact]
    public void IntegratesWithTheSemanticSaveRepository()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopSaveStorage(temporary.CreateDirectory("saves"));
        var repository = new SaveRepository(storage);
        var snapshot = new RuntimeSnapshot
        {
            Id = 42,
            ScriptLine = 17,
            CurrentLabel = "chapter_one",
            CurrentText = "A complete desktop save",
        };

        repository.Save(SaveTarget.Auto, snapshot);

        SaveLoadResult restored = repository.LoadAutosave(0);
        Assert.True(restored.IsSuccess);
        Assert.Equal(snapshot.Id, restored.Snapshot?.Id);
        Assert.Equal(snapshot.ScriptLine, restored.Snapshot?.ScriptLine);
        Assert.Equal(snapshot.CurrentLabel, restored.Snapshot?.CurrentLabel);
        Assert.Equal(snapshot.CurrentText, restored.Snapshot?.CurrentText);
    }

    [Fact]
    public void MissingFilesHaveNonThrowingInterfaceSemantics()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopSaveStorage(temporary.CreateDirectory("saves"));
        StoragePath path = StoragePath.Parse("missing.vsav");

        Assert.False(storage.Exists(path));
        Assert.False(storage.TryRead(path, out ReadOnlyMemory<byte> data));
        Assert.True(data.IsEmpty);
        Assert.False(storage.Delete(path));
        Assert.Empty(storage.List());
    }

    [Fact]
    public void AtomicallyOverwritesAndReadsBackCompleteData()
    {
        using var temporary = new TemporaryDirectory();
        string root = temporary.CreateDirectory("saves");
        var storage = new DesktopSaveStorage(root);
        StoragePath path = StoragePath.Parse("autosaves/save_0.vsav");
        byte[] original = Enumerable.Repeat((byte)0x11, 128 * 1024).ToArray();
        byte[] replacement = Enumerable.Repeat((byte)0xee, 192 * 1024).ToArray();

        storage.WriteAtomic(path, original);
        storage.WriteAtomic(path, replacement);

        Assert.True(storage.Exists(path));
        Assert.True(storage.TryRead(path, out ReadOnlyMemory<byte> restored));
        Assert.Equal(replacement, restored.ToArray());
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.GetDirectoryName(Path.Combine(root, path.Value))!),
            static file => Path.GetFileName(file).StartsWith(".velora-atomic-", StringComparison.Ordinal));
    }

    [Fact]
    public void ListsNestedFilesInPortableOrdinalOrder()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopSaveStorage(temporary.CreateDirectory("saves"));
        storage.WriteAtomic(StoragePath.Parse("zeta.vsav"), [1]);
        storage.WriteAtomic(StoragePath.Parse("autosaves/save_2.vsav"), [1, 2, 3]);
        storage.WriteAtomic(StoragePath.Parse("Alpha.vsav"), [1, 2]);

        IReadOnlyList<StorageEntry> entries = storage.List();

        Assert.Equal(
            ["Alpha.vsav", "autosaves/save_2.vsav", "zeta.vsav"],
            entries.Select(static entry => entry.Path.Value));
        Assert.Equal([2L, 3L, 1L], entries.Select(static entry => entry.Length));
        Assert.All(entries, static entry => Assert.NotNull(entry.LastModified));
    }

    [Fact]
    public void EnforcesConfiguredReadAndWriteBounds()
    {
        using var temporary = new TemporaryDirectory();
        string root = temporary.CreateDirectory("saves");
        var storage = new DesktopSaveStorage(root, maximumFileBytes: 4);

        storage.WriteAtomic(StoragePath.Parse("bounded.vsav"), [1, 2, 3, 4]);
        Assert.Throws<InvalidDataException>(() =>
            storage.WriteAtomic(StoragePath.Parse("too-large.vsav"), [1, 2, 3, 4, 5]));

        temporary.Write("saves/external.vsav", [1, 2, 3, 4, 5]);
        Assert.Throws<InvalidDataException>(() =>
            storage.TryRead(StoragePath.Parse("external.vsav"), out _));
    }

    [Fact]
    public void RejectsDefaultPathsAndReservedInternalNames()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopSaveStorage(temporary.CreateDirectory("saves"));

        Assert.Throws<ArgumentException>(() => storage.Exists(default));
        Assert.Throws<ArgumentException>(() =>
            storage.WriteAtomic(StoragePath.Parse(".velora-atomic-forged.tmp"), [1]));
        Assert.False(StoragePath.TryParse("../escape.vsav", out _));
        Assert.False(StoragePath.TryParse(Path.GetFullPath("escape.vsav"), out _));
    }

    [Fact]
    public void RequiresAnExplicitAbsoluteHostRootAndBoundedLimit()
    {
        Assert.Throws<ArgumentException>(() => new DesktopSaveStorage("relative-root"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DesktopSaveStorage(Path.GetTempPath(), maximumFileBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DesktopSaveStorage(
                Path.GetTempPath(),
                maximumFileBytes: DesktopStorageLimits.MaximumFileBytes + 1));
    }

    [Fact]
    public void RejectsDirectoryAndFileSymlinkEscapesAndHidesThemFromEnumeration()
    {
        using var temporary = new TemporaryDirectory();
        string root = temporary.CreateDirectory("saves");
        string outside = temporary.CreateDirectory("saves-other");
        temporary.Write("saves-other/secret.vsav", [9, 8, 7]);
        bool directoryLink = temporary.TryCreateDirectoryLink("saves/escape", outside);
        bool fileLink = temporary.TryCreateFileLink(
            "saves/file-escape.vsav",
            temporary.GetPath("saves-other/secret.vsav"));
        Assert.True(
            (directoryLink && fileLink) || OperatingSystem.IsWindows(),
            "Symbolic links must be available on Unix test hosts.");
        if (!directoryLink || !fileLink)
        {
            return;
        }

        var storage = new DesktopSaveStorage(root);
        StoragePath nestedEscape = StoragePath.Parse("escape/secret.vsav");
        StoragePath directEscape = StoragePath.Parse("file-escape.vsav");

        Assert.Throws<DesktopStorageContainmentException>(() => storage.Exists(nestedEscape));
        Assert.Throws<DesktopStorageContainmentException>(() => storage.TryRead(directEscape, out _));
        Assert.Throws<DesktopStorageContainmentException>(() => storage.WriteAtomic(nestedEscape, [1]));
        Assert.Throws<DesktopStorageContainmentException>(() => storage.Delete(directEscape));
        Assert.Empty(storage.List());
        Assert.Equal([9, 8, 7], File.ReadAllBytes(temporary.GetPath("saves-other/secret.vsav")));
    }

    [Fact]
    public void AllowsAContainedDirectorySymlinkAndStillUsesLogicalNames()
    {
        using var temporary = new TemporaryDirectory();
        string root = temporary.CreateDirectory("saves");
        string target = temporary.CreateDirectory("saves/real");
        temporary.Write("saves/real/inside.vsav", [4, 2]);
        bool linkCreated = temporary.TryCreateDirectoryLink("saves/alias", target);
        Assert.True(linkCreated || OperatingSystem.IsWindows(), "Symbolic links must be available on Unix test hosts.");
        if (!linkCreated)
        {
            return;
        }

        var storage = new DesktopSaveStorage(root);

        Assert.True(storage.TryRead(StoragePath.Parse("alias/inside.vsav"), out ReadOnlyMemory<byte> data));
        Assert.Equal([4, 2], data.ToArray());
        Assert.Contains(storage.List(), static entry => entry.Path.Value == "alias/inside.vsav");
    }

    [Fact]
    public void DeletesExistingFileWithoutDeletingDirectories()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopSaveStorage(temporary.CreateDirectory("saves"));
        StoragePath path = StoragePath.Parse("nested/save.vsav");
        storage.WriteAtomic(path, [1]);

        Assert.True(storage.Delete(path));
        Assert.False(storage.Delete(path));
        Assert.False(storage.Exists(path));
    }
}
