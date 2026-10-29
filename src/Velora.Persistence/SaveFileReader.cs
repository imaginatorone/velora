using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Validates a VSAV container and dispatches to its version-specific reader.</summary>
public static class SaveFileReader
{
    /// <summary>
    /// Reads a VSAV stream. If <paramref name="version1ByteOrder"/> is omitted, v1 uses the current
    /// process byte order, matching the historical C++ reader's host-native behavior.
    /// </summary>
    public static RuntimeSnapshot Read(
        Stream input,
        LegacyV1ByteOrder? version1ByteOrder = null)
    {
        VersionedPayload file = VersionedFile.Read(input, "VSAV"u8);
        return file.Version switch
        {
            1 => SaveFileReaderV1.Read(
                file.Payload.Span,
                version1ByteOrder ?? NativeByteOrder),
            2 => SaveFileReaderV2.Read(file.Payload.Span),
            _ => throw new InvalidDataException($"Unsupported VSAV version {file.Version}."),
        };
    }

    private static LegacyV1ByteOrder NativeByteOrder =>
        BitConverter.IsLittleEndian
            ? LegacyV1ByteOrder.LittleEndian
            : LegacyV1ByteOrder.BigEndian;
}
