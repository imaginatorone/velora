using System.Buffers.Binary;
using System.Text;

namespace Velora.Persistence;

internal sealed class CborWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly Stream _output;
    private int _bytesWritten;

    internal CborWriter(Stream output)
    {
        _output = output;
    }

    internal void WriteMapHeader(int count)
    {
        WriteCollectionHeader(5, count);
    }

    internal void WriteArrayHeader(int count)
    {
        WriteCollectionHeader(4, count);
    }

    internal void WriteTextString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int byteCount;
        try
        {
            byteCount = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException("Persistence text contains invalid UTF-16.", error);
        }

        WriteMajorValue(3, (ulong)byteCount);
        EnsureWithinLimit(byteCount);
        byte[] bytes = GC.AllocateUninitializedArray<byte>(byteCount);
        try
        {
            _ = StrictUtf8.GetBytes(value, bytes);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException("Persistence text contains invalid UTF-16.", error);
        }

        WriteBytes(bytes);
    }

    internal void WriteBoolean(bool value)
    {
        WriteByte(value ? (byte)0xf5 : (byte)0xf4);
    }

    internal void WriteUInt64(ulong value)
    {
        WriteMajorValue(0, value);
    }

    internal void WriteInt64(long value)
    {
        if (value >= 0)
        {
            WriteMajorValue(0, (ulong)value);
        }
        else
        {
            WriteMajorValue(1, (ulong)(-1L - value));
        }
    }

    internal void WriteDouble(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidDataException("Persistence numbers must be finite.");
        }

        Span<byte> encoded = stackalloc byte[9];
        encoded[0] = 0xfb;
        BinaryPrimitives.WriteUInt64BigEndian(encoded[1..], (ulong)BitConverter.DoubleToInt64Bits(value));
        WriteBytes(encoded);
    }

    internal void WriteSingle(float value)
    {
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("Persistence numbers must be finite.");
        }

        Span<byte> encoded = stackalloc byte[5];
        encoded[0] = 0xfa;
        BinaryPrimitives.WriteUInt32BigEndian(encoded[1..], (uint)BitConverter.SingleToInt32Bits(value));
        WriteBytes(encoded);
    }

    private void WriteCollectionHeader(int majorType, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        WriteMajorValue(majorType, (ulong)count);
    }

    private void WriteMajorValue(int majorType, ulong value)
    {
        Span<byte> encoded = stackalloc byte[9];
        int length;
        if (value < 24)
        {
            encoded[0] = (byte)((majorType << 5) | (byte)value);
            length = 1;
        }
        else if (value <= byte.MaxValue)
        {
            encoded[0] = (byte)((majorType << 5) | 24);
            encoded[1] = (byte)value;
            length = 2;
        }
        else if (value <= ushort.MaxValue)
        {
            encoded[0] = (byte)((majorType << 5) | 25);
            BinaryPrimitives.WriteUInt16BigEndian(encoded[1..], (ushort)value);
            length = 3;
        }
        else if (value <= uint.MaxValue)
        {
            encoded[0] = (byte)((majorType << 5) | 26);
            BinaryPrimitives.WriteUInt32BigEndian(encoded[1..], (uint)value);
            length = 5;
        }
        else
        {
            encoded[0] = (byte)((majorType << 5) | 27);
            BinaryPrimitives.WriteUInt64BigEndian(encoded[1..], value);
            length = 9;
        }

        WriteBytes(encoded[..length]);
    }

    private void WriteByte(byte value)
    {
        EnsureWithinLimit(1);
        _output.WriteByte(value);
        _bytesWritten++;
    }

    private void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        EnsureWithinLimit(bytes.Length);
        _output.Write(bytes);
        _bytesWritten += bytes.Length;
    }

    private void EnsureWithinLimit(int additionalBytes)
    {
        if (additionalBytes < 0 || _bytesWritten > PersistenceLimits.MaxPayloadBytes - additionalBytes)
        {
            throw new InvalidDataException("The persistence payload exceeds the 16 MiB file limit.");
        }
    }
}
