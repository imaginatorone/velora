using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Writes transition-era VSAV v2 files accepted by the current C++ reader.</summary>
public static class SaveFileWriterV2
{
    public static void Write(Stream output, RuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var payload = new MemoryStream();
        var writer = new CborWriter(payload);
        SnapshotCborCodec.Write(writer, snapshot);
        VersionedFile.Write(output, "VSAV"u8, version: 2, payload.GetBuffer().AsSpan(0, (int)payload.Length));
    }
}
