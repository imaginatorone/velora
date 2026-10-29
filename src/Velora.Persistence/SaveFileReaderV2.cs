using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Reads the bounded nlohmann/json-compatible CBOR payload used by VSAV v2.</summary>
public static class SaveFileReaderV2
{
    public static RuntimeSnapshot Read(ReadOnlySpan<byte> payload)
    {
        var reader = new CborReader(payload);
        RuntimeSnapshot snapshot = SnapshotCborCodec.Read(ref reader);
        if (!reader.End)
        {
            throw new InvalidDataException("The VSAV v2 payload contains trailing data.");
        }

        return snapshot;
    }
}
