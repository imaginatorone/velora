using System.Buffers;
using System.Buffers.Binary;

namespace Velora.Persistence;

internal static class VersionedFile
{
    internal const int HeaderLength = 6;

    internal static VersionedPayload Read(Stream input, ReadOnlySpan<byte> expectedMagic)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(input));
        }

        byte[] bytes = ReadAllBounded(input);
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException("The persistence file header is truncated.");
        }

        if (!bytes.AsSpan(0, 4).SequenceEqual(expectedMagic))
        {
            throw new InvalidDataException("The persistence file magic is invalid.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        return new VersionedPayload(version, bytes.AsMemory(HeaderLength));
    }

    internal static void Write(
        Stream output,
        ReadOnlySpan<byte> magic,
        ushort version,
        ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("The stream must be writable.", nameof(output));
        }

        if (magic.Length != 4)
        {
            throw new ArgumentException("Persistence magic must contain exactly four bytes.", nameof(magic));
        }

        if (payload.Length > PersistenceLimits.MaxPayloadBytes)
        {
            throw new InvalidDataException("The persistence payload exceeds the 16 MiB file limit.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], version);
        output.Write(header);
        output.Write(payload);
    }

    private static byte[] ReadAllBounded(Stream input)
    {
        if (input.CanSeek)
        {
            long remaining = input.Length - input.Position;
            if (remaining < 0 || remaining > PersistenceLimits.MaxFileBytes)
            {
                throw new InvalidDataException("The persistence file exceeds the 16 MiB limit.");
            }

            byte[] bytes = GC.AllocateUninitializedArray<byte>((int)remaining);
            input.ReadExactly(bytes);
            return bytes;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            using var result = new MemoryStream();
            while (true)
            {
                int read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    return result.ToArray();
                }

                if (result.Length + read > PersistenceLimits.MaxFileBytes)
                {
                    throw new InvalidDataException("The persistence file exceeds the 16 MiB limit.");
                }

                result.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

internal readonly record struct VersionedPayload(ushort Version, ReadOnlyMemory<byte> Payload);
