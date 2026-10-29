using Velora.Runtime.Contracts;

namespace Velora.Runtime.Contracts.Tests;

public sealed class RuntimeEventTests
{
    private static readonly InstructionId Instruction = InstructionId.FromOrdinal(11);

    [Fact]
    public void CompleteRuntimeEventVocabularyCarriesSequenceAndInstruction()
    {
        var dialogue = new DialogueState("mira", "Mira", "#fff", "Hi", 2, isComplete: true);
        var choice = new ChoiceState([new ChoiceOption(0, "Continue")]);
        var input = new TextInputState("name", "Name?");
        var sprite = new SpriteState { Id = "mira", ImagePath = "sprites/mira.png" };
        var transition = new TransitionIntent("fade", TimeSpan.FromMilliseconds(500));
        var audio = new AudioRequest("bgm", "audio/theme.ogg", 0.8f, loop: true, TimeSpan.Zero);
        var audioStop = new AudioStopRequest("bgm", TimeSpan.FromSeconds(1));
        var effect = new EffectRequest(
            EffectOperation.Apply,
            "vignette",
            0.5,
            TimeSpan.FromSeconds(2),
            new Dictionary<string, double>(StringComparer.Ordinal) { ["softness"] = 0.25 });

        RuntimeEvent[] events =
        [
            new DialogueStarted(1, Instruction, dialogue),
            new BackgroundChanged(2, Instruction, "backgrounds/room.png", transition),
            new SpriteShown(3, Instruction, sprite, transition),
            new SpriteHidden(4, Instruction, "mira", transition),
            new SpritePropertyChanged(5, Instruction, "mira", "alpha", 0.5),
            new ChoiceRequested(6, Instruction, choice),
            new TextInputRequested(7, Instruction, input),
            new AudioRequested(8, Instruction, audio),
            new AudioStopped(9, Instruction, audioStop),
            new EffectRequested(10, Instruction, effect),
            new VariableChanged(11, Instruction, "route", null, new RuntimeValue.StringValue("north")),
            new ExecutionPositionChanged(12, InstructionId.FromOrdinal(10), Instruction, "start"),
            new TitleChanged(13, Instruction, "Velora"),
            new SaveLockChanged(14, Instruction, isLocked: true),
            new QuitRequested(15, Instruction),
            new MainMenuRequested(16, Instruction),
            new RuntimeWarning(17, Instruction, "RUN001", "Unknown label; execution continues."),
        ];

        Assert.Equal(Enumerable.Range(1, events.Length).Select(value => (ulong)value), events.Select(value => value.Sequence));
        Assert.All(events, value => Assert.Equal(Instruction, value.Instruction));
        Assert.IsType<DialogueStarted>(events[0]);
        Assert.IsType<RuntimeWarning>(events[^1]);
    }

    [Fact]
    public void EffectRequestDetachesAndOrdersParameters()
    {
        var parameters = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["z"] = 2,
            ["a"] = 1,
        };
        var request = new EffectRequest(
            EffectOperation.Apply,
            "color_grade",
            1,
            TimeSpan.Zero,
            parameters);
        parameters.Clear();

        Assert.Equal(["a", "z"], request.Parameters.Keys);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, double>)request.Parameters).Add("new", 3));
    }

    [Fact]
    public void EventPayloadsRejectInvalidPresentationValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpritePropertyChanged(1, Instruction, "mira", "alpha", double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AudioRequest("bgm", "theme.ogg", float.PositiveInfinity, true, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EffectRequest(EffectOperation.Apply, "blur", 1.1, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(
            () => new ExecutionPositionChanged(1, Instruction, default, "start"));
        Assert.Throws<ArgumentException>(
            () => new RuntimeWarning(1, Instruction, string.Empty, "message"));
    }
}
