using Velora.Runtime.Contracts;

namespace Velora.Runtime.Tests;

public sealed class ContinuationTests
{
    [Fact]
    public void NestedDialogueSuspendsBeforeRemainderAndResumesExplicitContinuation()
    {
        const string source = """
            label start:
            if true:
                "Nested dialogue"
                set branch_after_dialogue = 1
            end
            set outer_after_if = 1
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);

        runtime.Start();

        Assert.Equal(RuntimeExecutionStatus.WaitingForAdvance, runtime.CaptureState().Execution.Status);
        Assert.False(runtime.CaptureState().Variables.ContainsKey("branch_after_dialogue"));
        Assert.False(runtime.CaptureState().Variables.ContainsKey("outer_after_if"));

        runtime.Advance();

        Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
        Assert.Equal(1, runtime.Number("branch_after_dialogue"));
        Assert.Equal(1, runtime.Number("outer_after_if"));
    }

    [Fact]
    public void NestedWaitSuspendsBeforeRemainderAndUsesDeterministicDelta()
    {
        const string source = """
            label start:
            if true:
                wait 2
                set branch_after_wait = 1
            end
            set outer_after_if = 1
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);
        runtime.Start();

        runtime.Update(TimeSpan.FromSeconds(1.25));

        RuntimeState partial = runtime.CaptureState();
        Assert.Equal(RuntimeExecutionStatus.WaitingForTime, partial.Execution.Status);
        Assert.Equal(TimeSpan.FromSeconds(0.75), partial.Execution.Wait?.Remaining);
        Assert.False(partial.Variables.ContainsKey("branch_after_wait"));

        runtime.Update(TimeSpan.FromSeconds(0.75));

        Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
        Assert.Equal(1, runtime.Number("branch_after_wait"));
        Assert.Equal(1, runtime.Number("outer_after_if"));
    }

    [Fact]
    public void SelectedChoiceDialogueDoesNotLoseItsPendingContinuation()
    {
        const string source = """
            label start:
            menu:
                "First":
                    "Nested choice dialogue"
                    set choice_after_dialogue = 1
                "Second":
                    set second_choice = 1
            end
            set outer_after_menu = 1
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);
        runtime.Start();

        runtime.SelectChoice(0);

        Assert.Equal(RuntimeExecutionStatus.WaitingForAdvance, runtime.CaptureState().Execution.Status);
        Assert.False(runtime.CaptureState().Variables.ContainsKey("choice_after_dialogue"));
        Assert.False(runtime.CaptureState().Variables.ContainsKey("outer_after_menu"));

        runtime.Advance();

        Assert.Equal(1, runtime.Number("choice_after_dialogue"));
        Assert.Equal(1, runtime.Number("outer_after_menu"));
        Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
    }
}
