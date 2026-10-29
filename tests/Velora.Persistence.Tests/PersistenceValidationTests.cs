using System.Buffers.Binary;
using System.Text;
using Velora.Runtime.Contracts;

namespace Velora.Persistence.Tests;

public sealed class PersistenceValidationTests
{
    [Fact]
    public void RejectsInvalidMagicAndUnsupportedVersion()
    {
        AssertInvalidVsav("NOPE", 2, [0xa0]);
        AssertInvalidVsav("VSAV", 99, [0xa0]);
    }

    [Fact]
    public void RejectsTruncatedOrTrailingV2Cbor()
    {
        AssertInvalidVsav("VSAV", 2, [0xa1, 0x62, (byte)'i', (byte)'d']);
        AssertInvalidVsav("VSAV", 2, [0xa0, 0x00]);
    }

    [Fact]
    public void RejectsWrongV2FieldType()
    {
        AssertInvalidVsav(
            "VSAV",
            2,
            [0xa1, 0x62, (byte)'i', (byte)'d', 0x61, (byte)'x']);
    }

    [Fact]
    public void RejectsV2CollectionsBeforeAllocatingBeyondTheirLimits()
    {
        AssertInvalidVsav(
            "VSAV",
            2,
            [0xa1, 0x69, .. "variables"u8, 0xb9, 0x27, 0x11]);
        AssertInvalidVsav(
            "VSAV",
            2,
            [0xa1, 0x67, .. "sprites"u8, 0x99, 0x03, 0xe9]);
        AssertInvalidVsav(
            "VSAV",
            2,
            [0xa1, 0x65, .. "audio"u8, 0x98, 0x81]);
    }

    [Fact]
    public void RejectsIndefiniteLengthCbor()
    {
        AssertInvalidVsav("VSAV", 2, [0xbf, 0xff]);
    }

    [Fact]
    public void RejectsOversizeContainerBeforePayloadParsing()
    {
        using var input = new MemoryStream(new byte[PersistenceLimits.MaxFileBytes + 1]);

        _ = Assert.Throws<InvalidDataException>(() => SaveFileReader.Read(input));
    }

    [Fact]
    public void RejectsV1OversizeStringVariableCountAndUnknownType()
    {
        AssertInvalidVsav("VSAV", 1, V1WithFirstStringLength(PersistenceLimits.MaxLegacyStringBytes + 1));
        AssertInvalidVsav("VSAV", 1, V1WithVariableCount(PersistenceLimits.MaxVariables + 1));
        AssertInvalidVsav("VSAV", 1, V1WithUnknownVariableType());
    }

    [Fact]
    public void RejectsNonFiniteV1VariableAndSpriteNumbers()
    {
        AssertInvalidVsav("VSAV", 1, V1WithNonFiniteVariable());
        AssertInvalidVsav("VSAV", 1, V1WithNonFiniteSprite());
    }

    [Fact]
    public void V1ReaderModelsBigEndianPayloadsExplicitly()
    {
        byte[] payload = EmptyV1Payload(LegacyV1ByteOrder.BigEndian, id: 7, scriptLine: 9);
        using MemoryStream input = Container("VSAV", 1, payload);

        RuntimeSnapshot snapshot = SaveFileReader.Read(input, LegacyV1ByteOrder.BigEndian);

        Assert.Equal(7UL, snapshot.Id);
        Assert.Equal(9UL, snapshot.ScriptLine);
    }

    [Fact]
    public void V1ReaderRejectsUnknownByteOrder()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => SaveFileReaderV1.Read([], (LegacyV1ByteOrder)99));
    }

    [Fact]
    public void V1ReaderPreservesLegacyTrailingByteTolerance()
    {
        byte[] original = File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "persistence",
                "synthetic-v1-host-native",
                "save_1.vsav"));
        byte[] extended = [.. original, 0xde, 0xad];
        using var input = new MemoryStream(extended);

        RuntimeSnapshot snapshot = SaveFileReader.Read(input, LegacyV1ByteOrder.LittleEndian);

        Assert.Equal(91UL, snapshot.ScriptLine);
    }

    [Fact]
    public void RejectsPersistentValueWhoseDeclaredTypeDoesNotMatchItsCborType()
    {
        byte[] payload =
        [
            0xa1,
            0x61, (byte)'x',
            0xa2,
            0x64, .. "type"u8,
            0x66, .. "number"u8,
            0x65, .. "value"u8,
            0xf4,
        ];
        using MemoryStream input = Container("VPER", 2, payload);

        _ = Assert.Throws<InvalidDataException>(() => PersistentFileReaderV2.Read(input));
    }

    [Fact]
    public void WritersRejectCountsAndNonFiniteNumbersOutsideTheContract()
    {
        var tooManySprites = new List<SpriteState>(PersistenceLimits.MaxSprites + 1);
        for (int index = 0; index <= PersistenceLimits.MaxSprites; index++)
        {
            tooManySprites.Add(new SpriteState());
        }

        var snapshot = new RuntimeSnapshot { Sprites = tooManySprites };
        using var snapshotOutput = new MemoryStream();
        _ = Assert.Throws<InvalidDataException>(() => SaveFileWriterV2.Write(snapshotOutput, snapshot));

        var persistent = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["invalid"] = new RuntimeValue.NumberValue(double.NaN),
        };
        using var persistentOutput = new MemoryStream();
        _ = Assert.Throws<InvalidDataException>(
            () => PersistentFileWriterV2.Write(persistentOutput, persistent));
    }

    private static void AssertInvalidVsav(string magic, ushort version, byte[] payload)
    {
        using MemoryStream input = Container(magic, version, payload);
        _ = Assert.Throws<InvalidDataException>(() => SaveFileReader.Read(input));
    }

    private static MemoryStream Container(string magic, ushort version, ReadOnlySpan<byte> payload)
    {
        byte[] bytes = new byte[6 + payload.Length];
        Encoding.ASCII.GetBytes(magic, bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), version);
        payload.CopyTo(bytes.AsSpan(6));
        return new MemoryStream(bytes);
    }

    private static byte[] V1WithFirstStringLength(int length)
    {
        byte[] payload = new byte[sizeof(ulong) * 2 + sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(sizeof(ulong) * 2), (uint)length);
        return payload;
    }

    private static byte[] V1WithVariableCount(int count)
    {
        byte[] payload = new byte[(sizeof(ulong) * 2) + (sizeof(uint) * 5)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(payload.Length - sizeof(uint)), (uint)count);
        return payload;
    }

    private static byte[] V1WithUnknownVariableType()
    {
        byte[] payload = new byte[(sizeof(ulong) * 2) + (sizeof(uint) * 7) + 2];
        int position = sizeof(ulong) * 2;
        position += sizeof(uint) * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(position), 1);
        position += sizeof(uint);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(position), 1);
        position += sizeof(uint);
        payload[position++] = (byte)'x';
        payload[position] = 99;
        return payload;
    }

    private static byte[] V1WithNonFiniteVariable()
    {
        byte[] payload = new byte[(sizeof(ulong) * 3) + (sizeof(uint) * 6) + 2];
        int position = sizeof(ulong) * 2;
        position += sizeof(uint) * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(position), 1);
        position += sizeof(uint);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(position), 1);
        position += sizeof(uint);
        payload[position++] = (byte)'x';
        payload[position++] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(
            payload.AsSpan(position),
            (ulong)BitConverter.DoubleToInt64Bits(double.NaN));
        return payload;
    }

    private static byte[] V1WithNonFiniteSprite()
    {
        byte[] payload = new byte[(sizeof(ulong) * 2) + (sizeof(uint) * 12) + 4];
        int position = sizeof(ulong) * 2;
        position += sizeof(uint) * 4;
        position += sizeof(uint); // zero variables
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(position), 1);
        position += sizeof(uint);
        position += sizeof(uint) * 2; // empty sprite id and image
        BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(position),
            (uint)BitConverter.SingleToInt32Bits(float.PositiveInfinity));
        return payload;
    }

    private static byte[] EmptyV1Payload(
        LegacyV1ByteOrder byteOrder,
        ulong id,
        ulong scriptLine)
    {
        byte[] payload = new byte[(sizeof(ulong) * 2) + (sizeof(uint) * 6)];
        WriteUInt64(payload, 0, id, byteOrder);
        WriteUInt64(payload, sizeof(ulong), scriptLine, byteOrder);
        return payload;
    }

    private static void WriteUInt64(
        Span<byte> destination,
        int offset,
        ulong value,
        LegacyV1ByteOrder byteOrder)
    {
        if (byteOrder == LegacyV1ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination[offset..], value);
        }
    }
}
