using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Writes nlohmann/json-compatible VPER v2 persistent-variable stores.</summary>
public static class PersistentFileWriterV2
{
    public static void Write(
        Stream output,
        IReadOnlyDictionary<string, RuntimeValue> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (variables.Count > PersistenceLimits.MaxVariables)
        {
            throw new InvalidDataException("The persistent-variable count exceeds the compatibility limit.");
        }

        using var payload = new MemoryStream();
        var writer = new CborWriter(payload);
        writer.WriteMapHeader(variables.Count);
        foreach ((string key, RuntimeValue value) in variables)
        {
            writer.WriteTextString(key);
            RuntimeValueCborCodec.Write(writer, value);
        }

        VersionedFile.Write(output, "VPER"u8, version: 2, payload.GetBuffer().AsSpan(0, (int)payload.Length));
    }
}
