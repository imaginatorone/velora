using Velora.Runtime.Contracts;

namespace Velora.Runtime.Contracts.Tests;

public sealed class InteractionAndExecutionStateTests
{
    private static readonly InstructionId Instruction = InstructionId.FromOrdinal(3);

    [Fact]
    public void DialogueRevealCountsUnicodeCodePointsRatherThanUtf16CodeUnits()
    {
        var dialogue = new DialogueState("mira", "Mira", "#ffffff", "A🌙", 2, isComplete: true);

        Assert.Equal(2, dialogue.RevealedCodePointCount);
        Assert.True(dialogue.IsComplete);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DialogueState("", "", "", "A🌙", 3, isComplete: false));
        Assert.Throws<ArgumentException>(
            () => new DialogueState("", "", "", "A🌙", 1, isComplete: true));
    }

    [Fact]
    public void ChoiceStateCopiesOptionsAndRequiresContiguousIndices()
    {
        var options = new List<ChoiceOption>
        {
            new(0, "North"),
            new(1, "South", isEnabled: false),
        };
        var choice = new ChoiceState(options);
        options.Clear();

        Assert.Equal(2, choice.Options.Count);
        Assert.False(choice.Options[1].IsEnabled);
        Assert.Throws<ArgumentException>(() => new ChoiceState([]));
        Assert.Throws<ArgumentException>(
            () => new ChoiceState([new ChoiceOption(1, "not zero")]));
    }

    [Fact]
    public void TimedExecutionRequiresMatchingWaitPayload()
    {
        var wait = new WaitState(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        var execution = new ExecutionState(
            RuntimeExecutionStatus.WaitingForTime,
            Instruction,
            "start",
            wait: wait);

        Assert.Same(wait, execution.Wait);
        Assert.Throws<ArgumentException>(
            () => new ExecutionState(
                RuntimeExecutionStatus.WaitingForTime,
                Instruction,
                "start"));
        Assert.Throws<ArgumentException>(
            () => new ExecutionState(
                RuntimeExecutionStatus.Running,
                Instruction,
                "start",
                wait: wait));
    }

    [Fact]
    public void ChoiceAndTextInputStatusesRequireOnlyTheirOwnPayload()
    {
        var choice = new ChoiceState([new ChoiceOption(0, "Continue")]);
        var input = new TextInputState("player_name", "Your name?");

        Assert.Same(
            choice,
            new ExecutionState(
                RuntimeExecutionStatus.WaitingForChoice,
                Instruction,
                "start",
                choice: choice).Choice);
        Assert.Same(
            input,
            new ExecutionState(
                RuntimeExecutionStatus.WaitingForTextInput,
                Instruction,
                "start",
                textInput: input).TextInput);
        Assert.Throws<ArgumentException>(
            () => new ExecutionState(
                RuntimeExecutionStatus.WaitingForChoice,
                Instruction,
                "start",
                choice: choice,
                textInput: input));
    }

    [Fact]
    public void ActiveExecutionRejectsDefaultInstructionButNotStartedMayUseIt()
    {
        Assert.Equal(
            RuntimeExecutionStatus.NotStarted,
            new ExecutionState(RuntimeExecutionStatus.NotStarted, default, string.Empty).Status);
        Assert.Throws<ArgumentException>(
            () => new ExecutionState(RuntimeExecutionStatus.Running, default, "start"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExecutionState(RuntimeExecutionStatus.Unknown, default, string.Empty));
    }

    [Fact]
    public void WaitAndTweenValidateTheirTemporalAndNumericState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WaitState(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TweenState(
                1,
                "mira",
                "alpha",
                0,
                1,
                double.NaN,
                TimeSpan.FromSeconds(1),
                TimeSpan.Zero,
                "linear"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TweenState(
                1,
                "mira",
                "alpha",
                0,
                1,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                "linear"));
    }
}
