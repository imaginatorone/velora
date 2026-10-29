using Velora.Runtime.Contracts;

namespace Velora.Persistence.Tests;

public sealed class LegacyPersistenceFixtureTests
{
    [Fact]
    public void ReadsAuthenticCppV2Snapshot()
    {
        using FileStream input = File.OpenRead(Fixture("cpp-v2", "save_1.vsav"));

        RuntimeSnapshot snapshot = SaveFileReader.Read(input);

        Assert.Equal(0UL, snapshot.Id);
        Assert.Equal(424_242UL, snapshot.ScriptLine);
        Assert.Equal("маршрут/arrival-🌙", snapshot.CurrentLabel);
        Assert.Equal("backgrounds/ночной-вокзал.png", snapshot.BackgroundPath);
        Assert.Equal(string.Empty, snapshot.CurrentSpeaker);
        Assert.Equal("Привет, 世界 — the fixture is alive. 🌙", snapshot.CurrentText);
        Assert.True(snapshot.TextComplete);
        Assert.True(snapshot.Timestamp > 0);

        Assert.Equal(string.Empty, StringValue(snapshot.Variables["empty_string"]));
        Assert.Equal("Значение / 値 / 🌙", StringValue(snapshot.Variables["unicode_string"]));
        Assert.Equal(-12_345.625, NumberValue(snapshot.Variables["number"]));
        Assert.True(BooleanValue(snapshot.Variables["boolean"]));

        Assert.Collection(
            snapshot.Sprites,
            hero =>
            {
                Assert.Equal("hero", hero.Id);
                Assert.Equal("sprites/героиня.png", hero.ImagePath);
                AssertClose(0.125f, hero.X);
                AssertClose(0.875f, hero.Y);
                AssertClose(0.25f, hero.Alpha);
                AssertClose(0.75f, hero.ScaleX);
                AssertClose(1.25f, hero.ScaleY);
                AssertClose(-15.5f, hero.Rotation);
                Assert.Equal(-3, hero.ZOrder);
                Assert.False(hero.Flipped);
            },
            rival =>
            {
                Assert.Equal("rival", rival.Id);
                Assert.Equal("sprites/rival.png", rival.ImagePath);
                AssertClose(-0.5f, rival.X);
                AssertClose(1.5f, rival.Y);
                AssertClose(0.875f, rival.Alpha);
                AssertClose(2.0f, rival.ScaleX);
                AssertClose(0.5f, rival.ScaleY);
                AssertClose(270.0f, rival.Rotation);
                Assert.Equal(42, rival.ZOrder);
                Assert.True(rival.Flipped);
            });

        Assert.Collection(
            snapshot.AudioTracks,
            bgm => AssertTrack(bgm, "bgm", "audio/ночная-тема.ogg", 12.5, 0.75f, true),
            sfx => AssertTrack(sfx, "sfx", string.Empty, 0.0, 0.0f, false),
            voice => AssertTrack(voice, "voice", "audio/voice/line-001.ogg", 123.456, 1.0f, true));
    }

    [Fact]
    public void ReadsAuthenticCppV2PersistentStore()
    {
        using FileStream input = File.OpenRead(Fixture("cpp-v2", "persistent.vsav"));

        IReadOnlyDictionary<string, RuntimeValue> variables = PersistentFileReaderV2.Read(input);

        Assert.Equal(4, variables.Count);
        Assert.Equal(string.Empty, StringValue(variables["persistent.empty"]));
        Assert.Equal("Сохранено / 保存 / ✓", StringValue(variables["persistent.unicode"]));
        Assert.Equal(-42.125, NumberValue(variables["persistent.number"]));
        Assert.False(BooleanValue(variables["persistent.boolean"]));
    }

    [Fact]
    public void ReadsExplicitlySyntheticLittleEndianV1ContractFixture()
    {
        using FileStream input = File.OpenRead(Fixture("synthetic-v1-host-native", "save_1.vsav"));

        RuntimeSnapshot snapshot = SaveFileReader.Read(input, LegacyV1ByteOrder.LittleEndian);

        Assert.Equal(0x1122_3344_5566_7788UL, snapshot.Id);
        Assert.Equal(91UL, snapshot.ScriptLine);
        Assert.Equal("synthetic-v1", snapshot.CurrentLabel);
        Assert.Equal(string.Empty, snapshot.BackgroundPath);
        Assert.Equal("Legacy Oracle", snapshot.CurrentSpeaker);
        Assert.Equal("Synthetic v1 fixture — не authentic user data", snapshot.CurrentText);
        Assert.False(snapshot.TextComplete);
        Assert.Equal(0, snapshot.Timestamp);
        Assert.Empty(snapshot.AudioTracks);
        Assert.Equal("строка v1", StringValue(snapshot.Variables["legacy.string"]));
        Assert.Equal(17.25, NumberValue(snapshot.Variables["legacy.number"]));
        Assert.True(BooleanValue(snapshot.Variables["legacy.boolean"]));

        SpriteState sprite = Assert.Single(snapshot.Sprites);
        Assert.Equal("legacy-hero", sprite.Id);
        Assert.Equal("sprites/legacy.png", sprite.ImagePath);
        AssertClose(-0.25f, sprite.X);
        AssertClose(1.25f, sprite.Y);
        AssertClose(0.625f, sprite.Alpha);
        AssertClose(1.5f, sprite.ScaleX);
        AssertClose(0.5f, sprite.ScaleY);
        Assert.Equal(0.0f, sprite.Rotation);
        Assert.Equal(0, sprite.ZOrder);
        Assert.False(sprite.Flipped);
    }

    [Fact]
    public void CSharpV2SnapshotRoundTripsEveryLegacyField()
    {
        RuntimeSnapshot expected = CreateInteropSnapshot();
        using var encoded = new MemoryStream();

        SaveFileWriterV2.Write(encoded, expected);
        encoded.Position = 0;
        RuntimeSnapshot actual = SaveFileReader.Read(encoded);

        AssertSnapshotEqual(expected, actual);
        Assert.Equal("VSAV"u8.ToArray(), encoded.GetBuffer().AsSpan(0, 4).ToArray());
        Assert.Equal(2, encoded.GetBuffer()[4]);
        Assert.Equal(0, encoded.GetBuffer()[5]);
    }

    [Fact]
    public void CSharpV2PersistentStoreRoundTripsEveryValueKind()
    {
        var expected = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["empty"] = new RuntimeValue.StringValue(string.Empty),
            ["unicode"] = new RuntimeValue.StringValue("Сохранено / 保存 / ✓"),
            ["number"] = new RuntimeValue.NumberValue(-42.125),
            ["boolean"] = new RuntimeValue.BooleanValue(false),
        };
        using var encoded = new MemoryStream();

        PersistentFileWriterV2.Write(encoded, expected);
        encoded.Position = 0;
        IReadOnlyDictionary<string, RuntimeValue> actual = PersistentFileReaderV2.Read(encoded);

        Assert.Equal(expected, actual);
    }

    internal static RuntimeSnapshot CreateInteropSnapshot()
    {
        return new RuntimeSnapshot
        {
            Id = 0,
            ScriptLine = 424_242,
            CurrentLabel = "маршрут/arrival-🌙",
            BackgroundPath = "backgrounds/ночной-вокзал.png",
            CurrentSpeaker = string.Empty,
            CurrentText = "Привет, 世界 — the fixture is alive. 🌙",
            TextComplete = true,
            Timestamp = 1_800_000_000,
            Variables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
            {
                ["empty_string"] = new RuntimeValue.StringValue(string.Empty),
                ["unicode_string"] = new RuntimeValue.StringValue("Значение / 値 / 🌙"),
                ["number"] = new RuntimeValue.NumberValue(-12_345.625),
                ["boolean"] = new RuntimeValue.BooleanValue(true),
            },
            Sprites =
            [
                new SpriteState
                {
                    Id = "hero", ImagePath = "sprites/героиня.png", X = 0.125f, Y = 0.875f,
                    Alpha = 0.25f, ScaleX = 0.75f, ScaleY = 1.25f, Rotation = -15.5f,
                    ZOrder = -3,
                },
                new SpriteState
                {
                    Id = "rival", ImagePath = "sprites/rival.png", X = -0.5f, Y = 1.5f,
                    Alpha = 0.875f, ScaleX = 2.0f, ScaleY = 0.5f, Rotation = 270.0f,
                    ZOrder = 42, Flipped = true,
                },
            ],
            AudioTracks =
            [
                new AudioTrackState
                {
                    Channel = "bgm", FilePath = "audio/ночная-тема.ogg", Position = 12.5,
                    Volume = 0.75f, Playing = true,
                },
                new AudioTrackState { Channel = "sfx", Volume = 0.0f },
                new AudioTrackState
                {
                    Channel = "voice", FilePath = "audio/voice/line-001.ogg", Position = 123.456,
                    Volume = 1.0f, Playing = true,
                },
            ],
        };
    }

    private static string Fixture(params string[] components)
    {
        return Path.Combine([AppContext.BaseDirectory, "fixtures", "persistence", .. components]);
    }

    private static string StringValue(RuntimeValue value)
    {
        return Assert.IsType<RuntimeValue.StringValue>(value).Value;
    }

    private static double NumberValue(RuntimeValue value)
    {
        return Assert.IsType<RuntimeValue.NumberValue>(value).Value;
    }

    private static bool BooleanValue(RuntimeValue value)
    {
        return Assert.IsType<RuntimeValue.BooleanValue>(value).Value;
    }

    private static void AssertTrack(
        AudioTrackState track,
        string channel,
        string file,
        double position,
        float volume,
        bool playing)
    {
        Assert.Equal(channel, track.Channel);
        Assert.Equal(file, track.FilePath);
        AssertClose(position, track.Position);
        AssertClose(volume, track.Volume);
        Assert.Equal(playing, track.Playing);
    }

    private static void AssertSnapshotEqual(RuntimeSnapshot expected, RuntimeSnapshot actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ScriptLine, actual.ScriptLine);
        Assert.Equal(expected.CurrentLabel, actual.CurrentLabel);
        Assert.Equal(expected.BackgroundPath, actual.BackgroundPath);
        Assert.Equal(expected.CurrentSpeaker, actual.CurrentSpeaker);
        Assert.Equal(expected.CurrentText, actual.CurrentText);
        Assert.Equal(expected.TextComplete, actual.TextComplete);
        Assert.Equal(expected.Timestamp, actual.Timestamp);
        Assert.Equal(expected.Variables, actual.Variables);
        Assert.Equal(expected.Sprites, actual.Sprites);
        Assert.Equal(expected.AudioTracks, actual.AudioTracks);
    }

    private static void AssertClose(float expected, float actual)
    {
        Assert.InRange(actual, expected - 0.000_001f, expected + 0.000_001f);
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.InRange(actual, expected - 0.000_001, expected + 0.000_001);
    }
}
