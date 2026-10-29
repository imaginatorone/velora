namespace Velora.Platform;

public sealed record StorageEntry
{
    public StorageEntry(StoragePath path, long length, DateTimeOffset? lastModified)
    {
        if (!path.IsValid)
        {
            throw new ArgumentException("A valid storage path is required.", nameof(path));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Path = path;
        Length = length;
        LastModified = lastModified;
    }

    public StoragePath Path { get; }

    public long Length { get; }

    public DateTimeOffset? LastModified { get; }
}

/// <summary>
/// Host-owned save storage. WriteAtomic must leave either the previous complete
/// value or the new complete value after interruption; partial saves are never
/// observable through this interface.
/// </summary>
public interface ISaveStorage
{
    bool Exists(StoragePath path);

    bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data);

    void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data);

    bool Delete(StoragePath path);

    IReadOnlyList<StorageEntry> List();
}
