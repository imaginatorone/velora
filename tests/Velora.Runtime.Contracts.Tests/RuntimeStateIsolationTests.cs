using System.Collections;
using Velora.Runtime.Contracts;

namespace Velora.Runtime.Contracts.Tests;

public sealed class RuntimeStateIsolationTests
{
    [Fact]
    public void RuntimeStateDetachesAndDeterministicallyOrdersMutableInputs()
    {
        var variables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["z"] = new RuntimeValue.BooleanValue(true),
            ["a"] = new RuntimeValue.NumberValue(1),
        };
        var history = new List<DialogueHistoryEntry>
        {
            new("mira", "Mira", "#fff", "Hello"),
        };
        var tweens = new List<TweenState>
        {
            Tween(2),
            Tween(1),
        };
        var tracks = new List<AudioTrackState>
        {
            new() { Channel = "voice", FilePath = "voice.ogg" },
            new() { Channel = "bgm", FilePath = "music.ogg" },
        };

        RuntimeState state = CreateState(variables, history, tweens, tracks);
        variables.Clear();
        history.Clear();
        tweens.Clear();
        tracks.Clear();

        Assert.Equal(["a", "z"], state.Variables.Keys);
        Assert.Single(state.DialogueHistory);
        Assert.Equal([1UL, 2UL], state.ActiveTweens.Select(tween => tween.Id));
        Assert.Equal(["bgm", "voice"], state.AudioTracks.Select(track => track.Channel));
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, RuntimeValue>)state.Variables).Add(
                "new",
                new RuntimeValue.BooleanValue(false)));
        Assert.Throws<NotSupportedException>(
            () => ((IList)state.ActiveTweens).Add(Tween(4)));
    }

    [Fact]
    public void SceneCopiesSpritesAndUsesStableDrawOrdering()
    {
        var sprites = new List<SpriteState>
        {
            Sprite("zeta", zOrder: 2),
            Sprite("beta", zOrder: 1),
            Sprite("alpha", zOrder: 1),
        };
        var scene = new SceneState("backgrounds/room.png", sprites);
        sprites.Clear();

        Assert.Equal(["alpha", "beta", "zeta"], scene.Sprites.Select(sprite => sprite.Id));
        Assert.Throws<NotSupportedException>(
            () => ((IList)scene.Sprites).Add(Sprite("extra", zOrder: 0)));
        Assert.Throws<ArgumentException>(
            () => new SceneState(string.Empty, [Sprite("same", 0), Sprite("same", 1)]));
    }

    [Fact]
    public void RuntimeStateRejectsInvalidRuntimeValuesAndDuplicateTweenIds()
    {
        var invalidVariables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["bad"] = new RuntimeValue.NumberValue(double.PositiveInfinity),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateState(invalidVariables));
        Assert.Throws<ArgumentException>(
            () => CreateState(
                new Dictionary<string, RuntimeValue>(StringComparer.Ordinal),
                tweens: [Tween(1), Tween(1)]));
    }

    [Fact]
    public void CompatibilitySnapshotAlsoDetachesItsCollections()
    {
        var variables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["route"] = new RuntimeValue.StringValue("north"),
        };
        var sprites = new List<SpriteState> { Sprite("mira", 0) };
        var tracks = new List<AudioTrackState>
        {
            new() { Channel = "bgm", FilePath = "music.ogg" },
        };
        var snapshot = new RuntimeSnapshot
        {
            Variables = variables,
            Sprites = sprites,
            AudioTracks = tracks,
        };

        variables.Clear();
        sprites.Clear();
        tracks.Clear();

        Assert.Equal("north", Assert.IsType<RuntimeValue.StringValue>(snapshot.Variables["route"]).Value);
        Assert.Single(snapshot.Sprites);
        Assert.Single(snapshot.AudioTracks);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, RuntimeValue>)snapshot.Variables).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot.Sprites).Clear());
    }

    [Fact]
    public void CompatibilitySnapshotRejectsNullAndNonFiniteCollectionPayloads()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeSnapshot
            {
                Variables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
                {
                    ["missing"] = null!,
                },
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeSnapshot
            {
                Variables = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
                {
                    ["invalid"] = new RuntimeValue.NumberValue(double.NaN),
                },
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeSnapshot
            {
                Sprites = [Sprite("mira", 0) with { Alpha = float.PositiveInfinity }],
            });
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeSnapshot { AudioTracks = [null!] });
    }

    private static RuntimeState CreateState(
        IReadOnlyDictionary<string, RuntimeValue> variables,
        IEnumerable<DialogueHistoryEntry>? history = null,
        IEnumerable<TweenState>? tweens = null,
        IEnumerable<AudioTrackState>? tracks = null)
    {
        var fingerprint = new ProgramFingerprint(
            "compiler-1",
            new string('1', ProgramFingerprint.ContentHashLength));
        var execution = new ExecutionState(
            RuntimeExecutionStatus.Running,
            InstructionId.FromOrdinal(7),
            "start");
        return new RuntimeState(
            fingerprint,
            execution,
            variables,
            new SceneState(string.Empty, []),
            new DeterministicState(123, 456, 9),
            dialogueHistory: history,
            activeTweens: tweens,
            audioTracks: tracks,
            title: "Velora",
            isSaveLocked: true);
    }

    private static TweenState Tween(ulong id) =>
        new(
            id,
            "mira",
            "alpha",
            0,
            1,
            0.5,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(500),
            "linear");

    private static SpriteState Sprite(string id, int zOrder) =>
        new()
        {
            Id = id,
            ImagePath = $"sprites/{id}.png",
            ZOrder = zOrder,
        };
}
