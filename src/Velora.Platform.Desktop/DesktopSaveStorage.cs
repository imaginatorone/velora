namespace Velora.Platform.Desktop;

/// <summary>
/// Canonically contained, bounded, atomic filesystem storage for desktop save
/// files. Host paths remain behind the platform abstraction.
/// </summary>
public sealed class DesktopSaveStorage : ISaveStorage
{
    private readonly ContainedFileStore _store;

    public DesktopSaveStorage(
        string rootDirectory,
        int maximumFileBytes = DesktopStorageLimits.MaximumFileBytes)
    {
        _store = new ContainedFileStore(rootDirectory, maximumFileBytes);
    }

    public bool Exists(StoragePath path) => _store.Exists(path);

    public bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data) =>
        _store.TryRead(path, out data);

    public void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data) =>
        _store.WriteAtomic(path, data);

    public bool Delete(StoragePath path) => _store.Delete(path);

    public IReadOnlyList<StorageEntry> List() => _store.List();
}
