namespace Velora.Platform;

/// <summary>
/// Sandboxed storage available to controlled VelScript meta commands. It does
/// not expose host paths, enumeration, process launch, or network access.
/// </summary>
public interface IScriptDataStore
{
    bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data);

    void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data);

    bool Delete(StoragePath path);
}
