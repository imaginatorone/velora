using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Reads the VPER v2 persistent-variable store.</summary>
public static class PersistentFileReaderV2
{
    public static IReadOnlyDictionary<string, RuntimeValue> Read(Stream input)
    {
        VersionedPayload file = VersionedFile.Read(input, "VPER"u8);
        if (file.Version != 2)
        {
            throw new InvalidDataException($"Unsupported VPER version {file.Version}.");
        }

        var reader = new CborReader(file.Payload.Span);
        int count = reader.ReadMapLength(PersistenceLimits.MaxVariables);
        var variables = new Dictionary<string, RuntimeValue>(count, StringComparer.Ordinal);
        for (int index = 0; index < count; index++)
        {
            string key = reader.ReadTextString();
            variables[key] = RuntimeValueCborCodec.Read(ref reader);
        }

        if (!reader.End)
        {
            throw new InvalidDataException("The VPER v2 payload contains trailing data.");
        }

        return variables;
    }
}
