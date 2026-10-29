namespace Velora.Platform.Desktop;

/// <summary>
/// Restricted per-game data store used by controlled VelScript file commands.
/// It intentionally exposes neither enumeration nor host filesystem paths.
/// </summary>
public sealed class DesktopScriptDataStore : IScriptDataStore
{
    private readonly ContainedFileStore _store;

    public DesktopScriptDataStore(
        string rootDirectory,
        int maximumFileBytes = DesktopStorageLimits.MaximumFileBytes)
    {
        _store = new ContainedFileStore(rootDirectory, maximumFileBytes);
    }

    public bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data) =>
        _store.TryRead(path, out data);

    public void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data) =>
        _store.WriteAtomic(path, data);

    public bool Delete(StoragePath path) => _store.Delete(path);
}
