using System.Buffers.Binary;
using System.Text;

namespace Velora.Persistence;

/// <summary>
/// Small bounded CBOR reader for the definite-length subset emitted by nlohmann/json. It avoids
/// constructing a general-purpose object graph before persistence schema limits are checked.
/// </summary>
internal ref struct CborReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private ReadOnlySpan<byte> _remaining;

    internal CborReader(ReadOnlySpan<byte> bytes)
    {
        _remaining = bytes;
    }

    internal bool End => _remaining.IsEmpty;

    internal int PeekMajorType()
    {
        EnsureAvailable(1);
        return _remaining[0] >> 5;
    }

    internal int PeekAdditionalInformation()
    {
        EnsureAvailable(1);
        return _remaining[0] & 0x1f;
    }

    internal int ReadMapLength(int maximum)
    {
        return ReadCollectionLength(expectedMajorType: 5, maximum, "map");
    }

    internal int ReadArrayLength(int maximum)
    {
        return ReadCollectionLength(expectedMajorType: 4, maximum, "array");
    }

    internal string ReadTextString(int maximumUtf8Bytes = PersistenceLimits.MaxPayloadBytes)
    {
        (int majorType, int additionalInformation) = ReadInitialByte();
        if (majorType != 3)
        {
            throw new InvalidDataException("Expected a CBOR text string.");
        }

        ulong byteLength = ReadAdditionalValue(additionalInformation);
        if (byteLength > (ulong)maximumUtf8Bytes || byteLength > (ulong)_remaining.Length)
        {
            throw new InvalidDataException("The CBOR text string is truncated or too large.");
        }

        ReadOnlySpan<byte> utf8 = Consume((int)byteLength);
        try
        {
            return StrictUtf8.GetString(utf8);
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException("The CBOR text string is not valid UTF-8.", error);
        }
    }

    internal bool ReadBoolean()
    {
        byte value = ReadByte();
        return value switch
        {
            0xf4 => false,
            0xf5 => true,
            _ => throw new InvalidDataException("Expected a CBOR boolean."),
        };
    }

    internal ulong ReadUInt64()
    {
        (int majorType, int additionalInformation) = ReadInitialByte();
        if (majorType != 0)
        {
            throw new InvalidDataException("Expected a non-negative CBOR integer.");
        }

        return ReadAdditionalValue(additionalInformation);
    }

    internal long ReadInt64()
    {
        (int majorType, int additionalInformation) = ReadInitialByte();
        ulong encoded = ReadAdditionalValue(additionalInformation);
        if (majorType == 0 && encoded <= long.MaxValue)
        {
            return (long)encoded;
        }

        if (majorType == 1 && encoded <= long.MaxValue)
        {
            return -1L - (long)encoded;
        }

        throw new InvalidDataException("The CBOR integer is outside Int64 range.");
    }

    internal int ReadInt32()
    {
        long value = ReadInt64();
        if (value is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException("The CBOR integer is outside Int32 range.");
        }

        return (int)value;
    }

    internal double ReadNumber()
    {
        (int majorType, int additionalInformation) = ReadInitialByte();
        double value = majorType switch
        {
            0 => ReadAdditionalValue(additionalInformation),
            1 => -1.0 - ReadAdditionalValue(additionalInformation),
            7 when additionalInformation == 25 => (double)BitConverter.UInt16BitsToHalf(ReadUInt16()),
            7 when additionalInformation == 26 => BitConverter.Int32BitsToSingle((int)ReadUInt32()),
            7 when additionalInformation == 27 => BitConverter.Int64BitsToDouble((long)ReadUInt64BigEndian()),
            _ => throw new InvalidDataException("Expected a CBOR number."),
        };

        if (!double.IsFinite(value))
        {
            throw new InvalidDataException("Persistence numbers must be finite.");
        }

        return value;
    }

    internal float ReadSingle()
    {
        double value = ReadNumber();
        if (value is < -float.MaxValue or > float.MaxValue)
        {
            throw new InvalidDataException("The CBOR number is outside Single range.");
        }

        return (float)value;
    }

    internal void SkipValue(int depth = 0)
    {
        if (depth > PersistenceLimits.MaxCborDepth)
        {
            throw new InvalidDataException("The CBOR nesting depth exceeds the persistence limit.");
        }

        (int majorType, int additionalInformation) = ReadInitialByte();
        switch (majorType)
        {
            case 0:
            case 1:
                _ = ReadAdditionalValue(additionalInformation);
                return;
            case 2:
            case 3:
                SkipBytes(ReadAdditionalValue(additionalInformation));
                return;
            case 4:
            {
                int count = CheckedCollectionLength(
                    ReadAdditionalValue(additionalInformation),
                    PersistenceLimits.MaxUnknownCollectionItems,
                    "array");
                for (int index = 0; index < count; index++)
                {
                    SkipValue(depth + 1);
                }

                return;
            }
            case 5:
            {
                int count = CheckedCollectionLength(
                    ReadAdditionalValue(additionalInformation),
                    PersistenceLimits.MaxUnknownCollectionItems,
                    "map");
                for (int index = 0; index < count; index++)
                {
                    SkipValue(depth + 1);
                    SkipValue(depth + 1);
                }

                return;
            }
            case 6:
                _ = ReadAdditionalValue(additionalInformation);
                SkipValue(depth + 1);
                return;
            case 7:
                SkipSimpleValuePayload(additionalInformation);
                return;
            default:
                throw new InvalidDataException("Unknown CBOR major type.");
        }
    }

    private int ReadCollectionLength(int expectedMajorType, int maximum, string kind)
    {
        (int majorType, int additionalInformation) = ReadInitialByte();
        if (majorType != expectedMajorType)
        {
            throw new InvalidDataException($"Expected a CBOR {kind}.");
        }

        return CheckedCollectionLength(ReadAdditionalValue(additionalInformation), maximum, kind);
    }

    private static int CheckedCollectionLength(ulong length, int maximum, string kind)
    {
        if (length > (ulong)maximum)
        {
            throw new InvalidDataException($"The CBOR {kind} exceeds its item limit.");
        }

        return (int)length;
    }

    private (int MajorType, int AdditionalInformation) ReadInitialByte()
    {
        byte initial = ReadByte();
        return (initial >> 5, initial & 0x1f);
    }

    private ulong ReadAdditionalValue(int additionalInformation)
    {
        return additionalInformation switch
        {
            < 24 => (ulong)additionalInformation,
            24 => ReadByte(),
            25 => ReadUInt16(),
            26 => ReadUInt32(),
            27 => ReadUInt64BigEndian(),
            31 => throw new InvalidDataException("Indefinite-length CBOR is not supported by VSAV v2."),
            _ => throw new InvalidDataException("The CBOR additional-information value is reserved."),
        };
    }

    private void SkipSimpleValuePayload(int additionalInformation)
    {
        switch (additionalInformation)
        {
            case < 24:
                return;
            case 24:
                _ = ReadByte();
                return;
            case 25:
                _ = ReadUInt16();
                return;
            case 26:
                _ = ReadUInt32();
                return;
            case 27:
                _ = ReadUInt64BigEndian();
                return;
            default:
                throw new InvalidDataException("Invalid CBOR simple value.");
        }
    }

    private void SkipBytes(ulong length)
    {
        if (length > (ulong)_remaining.Length)
        {
            throw new InvalidDataException("The CBOR value is truncated.");
        }

        _ = Consume((int)length);
    }

    private byte ReadByte()
    {
        EnsureAvailable(1);
        byte value = _remaining[0];
        _remaining = _remaining[1..];
        return value;
    }

    private ushort ReadUInt16()
    {
        return BinaryPrimitives.ReadUInt16BigEndian(Consume(sizeof(ushort)));
    }

    private uint ReadUInt32()
    {
        return BinaryPrimitives.ReadUInt32BigEndian(Consume(sizeof(uint)));
    }

    private ulong ReadUInt64BigEndian()
    {
        return BinaryPrimitives.ReadUInt64BigEndian(Consume(sizeof(ulong)));
    }

    private ReadOnlySpan<byte> Consume(int length)
    {
        EnsureAvailable(length);
        ReadOnlySpan<byte> value = _remaining[..length];
        _remaining = _remaining[length..];
        return value;
    }

    private void EnsureAvailable(int length)
    {
        if (length < 0 || _remaining.Length < length)
        {
            throw new InvalidDataException("The CBOR payload is truncated.");
        }
    }
}
