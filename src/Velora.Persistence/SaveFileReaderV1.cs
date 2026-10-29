using System.Buffers.Binary;
using System.Text;
using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>Reads the host-native payload used by VSAV v1.</summary>
public static class SaveFileReaderV1
{
    /// <summary>
    /// Reads a v1 payload after the six-byte VSAV header. Trailing bytes are accepted because the
    /// C++ compatibility reader accepted them. The caller must supply the original host byte order;
    /// v1 did not persist it.
    /// </summary>
    public static RuntimeSnapshot Read(ReadOnlySpan<byte> payload, LegacyV1ByteOrder byteOrder)
    {
        if (byteOrder is not (LegacyV1ByteOrder.LittleEndian or LegacyV1ByteOrder.BigEndian))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown legacy byte order.");
        }

        var reader = new LegacyBinaryReader(payload, byteOrder);
        ulong id = reader.ReadUInt64();
        ulong scriptLine = reader.ReadUInt64();
        string label = reader.ReadString();
        string background = reader.ReadString();
        string speaker = reader.ReadString();
        string text = reader.ReadString();

        uint variableCount = reader.ReadUInt32();
        if (variableCount > PersistenceLimits.MaxVariables)
        {
            throw new InvalidDataException("The VSAV v1 variable count exceeds the compatibility limit.");
        }

        var variables = new Dictionary<string, RuntimeValue>((int)variableCount, StringComparer.Ordinal);
        for (uint index = 0; index < variableCount; index++)
        {
            string name = reader.ReadString();
            RuntimeValue value = reader.ReadByte() switch
            {
                0 => new RuntimeValue.StringValue(reader.ReadString()),
                1 => new RuntimeValue.NumberValue(reader.ReadDouble()),
                2 => new RuntimeValue.BooleanValue(reader.ReadByte() != 0),
                _ => throw new InvalidDataException("The VSAV v1 variable type tag is unknown."),
            };
            variables[name] = value;
        }

        uint spriteCount = reader.ReadUInt32();
        if (spriteCount > PersistenceLimits.MaxSprites)
        {
            throw new InvalidDataException("The VSAV v1 sprite count exceeds the compatibility limit.");
        }

        var sprites = new List<SpriteState>((int)spriteCount);
        for (uint index = 0; index < spriteCount; index++)
        {
            sprites.Add(new SpriteState
            {
                Id = reader.ReadString(),
                ImagePath = reader.ReadString(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Alpha = reader.ReadSingle(),
                ScaleX = reader.ReadSingle(),
                ScaleY = reader.ReadSingle(),
            });
        }

        return new RuntimeSnapshot
        {
            Id = id,
            ScriptLine = scriptLine,
            CurrentLabel = label,
            Variables = variables,
            BackgroundPath = background,
            Sprites = sprites,
            CurrentSpeaker = speaker,
            CurrentText = text,
        };
    }

    private ref struct LegacyBinaryReader
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly LegacyV1ByteOrder _byteOrder;
        private ReadOnlySpan<byte> _remaining;

        internal LegacyBinaryReader(ReadOnlySpan<byte> bytes, LegacyV1ByteOrder byteOrder)
        {
            _remaining = bytes;
            _byteOrder = byteOrder;
        }

        internal byte ReadByte()
        {
            return Consume(1)[0];
        }

        internal uint ReadUInt32()
        {
            ReadOnlySpan<byte> encoded = Consume(sizeof(uint));
            return _byteOrder == LegacyV1ByteOrder.LittleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(encoded)
                : BinaryPrimitives.ReadUInt32BigEndian(encoded);
        }

        internal ulong ReadUInt64()
        {
            ReadOnlySpan<byte> encoded = Consume(sizeof(ulong));
            return _byteOrder == LegacyV1ByteOrder.LittleEndian
                ? BinaryPrimitives.ReadUInt64LittleEndian(encoded)
                : BinaryPrimitives.ReadUInt64BigEndian(encoded);
        }

        internal float ReadSingle()
        {
            float value = BitConverter.Int32BitsToSingle((int)ReadUInt32());
            if (!float.IsFinite(value))
            {
                throw new InvalidDataException("VSAV v1 sprite numbers must be finite.");
            }

            return value;
        }

        internal double ReadDouble()
        {
            double value = BitConverter.Int64BitsToDouble((long)ReadUInt64());
            if (!double.IsFinite(value))
            {
                throw new InvalidDataException("VSAV v1 variable numbers must be finite.");
            }

            return value;
        }

        internal string ReadString()
        {
            uint length = ReadUInt32();
            if (length > PersistenceLimits.MaxLegacyStringBytes || length > (uint)_remaining.Length)
            {
                throw new InvalidDataException("The VSAV v1 string is truncated or exceeds 4 MiB.");
            }

            try
            {
                return StrictUtf8.GetString(Consume((int)length));
            }
            catch (DecoderFallbackException error)
            {
                throw new InvalidDataException("The VSAV v1 string is not valid UTF-8.", error);
            }
        }

        private ReadOnlySpan<byte> Consume(int length)
        {
            if (length < 0 || _remaining.Length < length)
            {
                throw new InvalidDataException("The VSAV v1 payload is truncated.");
            }

            ReadOnlySpan<byte> value = _remaining[..length];
            _remaining = _remaining[length..];
            return value;
        }
    }
}
