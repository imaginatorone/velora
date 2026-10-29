using Velora.Runtime.Contracts;

namespace Velora.Runtime.Tests;

public sealed class RuntimeStateTests
{
    [Fact]
    public void RestoreContinuesNestedWaitAndTweenWithoutReplayingEvents()
    {
        const string source = """
            character guide = "Guide"
            label start:
            show guide "guide.png" at left
            tween guide x 0.75 duration(2) linear
            if true:
                wait 1
                guide "Halfway"
                set done = true
            end
            """;
        StoryRuntime original = RuntimeTestHelper.Create(source);
        original.Start();
        original.Update(TimeSpan.FromSeconds(0.25));
        RuntimeState saved = original.CaptureState();
        StoryRuntime restored = RuntimeTestHelper.Create(source);
        restored.Restore(saved);
        Assert.Empty(restored.LastEvents);
        Assert.Equal(original.Update(TimeSpan.FromSeconds(0.75)), restored.Update(TimeSpan.FromSeconds(0.75)));
        Assert.Equal(0.5f, Assert.Single(restored.CaptureState().Scene.Sprites).X);
        Assert.Equal("Guide", restored.CaptureState().Dialogue!.SpeakerName);
        Assert.Equal(original.Advance(), restored.Advance());
        Assert.True(restored.Boolean("done"));
    }

    [Fact]
    public void RestoreKeepsExecutedDefinitionsAcrossBackwardJumps()
    {
        const string source = """
            label earlier:
            guide "Back at an earlier instruction"
            jump end_story
            label start:
            character guide = "Executed definition"
            jump earlier
            label end_story:
            guide "Still registered"
            """;
        StoryRuntime original = RuntimeTestHelper.Create(source);
        original.Start();
        StoryRuntime restored = RuntimeTestHelper.Create(source);
        restored.Restore(original.CaptureState());
        restored.Advance();
        Assert.Equal("Executed definition", restored.CaptureState().Dialogue!.SpeakerName);
    }

    [Fact]
    public void RestoreRejectsForeignProgramBeforeMutatingState()
    {
        StoryRuntime original = RuntimeTestHelper.Create("label start:\nset n = 1\n");
        StoryRuntime other = RuntimeTestHelper.Create("label start:\nset n = 2\n");
        original.Start();
        other.Start();
        Assert.Throws<ArgumentException>(() => original.Restore(other.CaptureState()));
        Assert.Equal(1, original.Number("n"));
    }

    [Fact]
    public void EmotionReturnLegSurvivesCaptureAndLargeDelta()
    {
        const string source = """
            label start:
            show guide "guide.png"
            emotion guide happy
            "Pause"
            """;
        StoryRuntime original = RuntimeTestHelper.Create(source);
        original.Start();
        original.Update(TimeSpan.FromSeconds(0.1));
        StoryRuntime restored = RuntimeTestHelper.Create(source);
        restored.Restore(original.CaptureState());
        restored.Update(TimeSpan.FromSeconds(0.5));
        Assert.Empty(restored.CaptureState().ActiveTweens);
        Assert.Equal(0.5f, Assert.Single(restored.CaptureState().Scene.Sprites).Y);
    }

    [Fact]
    public void KitchenSinkCompletesBothRoutesWithDeterministicInputs()
    {
        string root = FindRoot();
        string source = File.ReadAllText(Path.Combine(root, "samples", "KitchenSink", "Scripts", "main.vel"));
        for (int route = 0; route < 2; route++)
        {
            StoryRuntime runtime = RuntimeTestHelper.Create(source);
            runtime.Start();
            bool selectedRoute = false;
            for (int step = 0; step < 100; step++)
            {
                RuntimeState state = runtime.CaptureState();
                if (state.Execution.Status == RuntimeExecutionStatus.Finished)
                {
                    break;
                }

                switch (state.Execution.Status)
                {
                    case RuntimeExecutionStatus.WaitingForAdvance: runtime.Advance(); break;
                    case RuntimeExecutionStatus.WaitingForTextInput: runtime.SubmitTextInput("Ada"); break;
                    case RuntimeExecutionStatus.WaitingForTime: runtime.Update(TimeSpan.FromSeconds(1)); break;
                    case RuntimeExecutionStatus.WaitingForChoice:
                        bool routeMenu = state.Execution.Choice!.Options[0].Text == "Run the complete showcase";
                        runtime.SelectChoice(routeMenu ? route : 0);
                        selectedRoute |= routeMenu;
                        break;
                    default: Assert.Fail($"Unexpected state {state.Execution.Status}"); break;
                }
            }

            Assert.True(selectedRoute);
            Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
            if (route == 0)
            {
                Assert.True(runtime.Boolean("nested_after_dialogue"));
                Assert.True(runtime.Boolean("nested_after_wait"));
                Assert.True(runtime.Boolean("choice_after_dialogue"));
                Assert.Empty(runtime.CaptureState().Particles);
                Assert.Empty(runtime.CaptureState().ActiveScreens);
            }
            else
            {
                Assert.Equal(3.5, runtime.Number("arithmetic"));
            }
        }
    }

    [Fact]
    public void HostSideEffectsAreOnlyEventsAndRestoreNeverRepeatsThem()
    {
        const string source = """
            label start:
            meta file_write "story.txt" "test"
            meta fullscreen
            "pause"
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);
        Assert.Equal(2, runtime.Start().OfType<HostActionRequested>().Count());
        RuntimeState state = runtime.CaptureState();
        runtime.Restore(state);
        Assert.Empty(runtime.LastEvents);
        Assert.Empty(runtime.Advance().OfType<HostActionRequested>());
    }

    [Fact]
    public void InterpolationIsSinglePassAndUnknownNamesHaveLegacyEmptyDisplay()
    {
        const string source = """
            set nested = "[other]"
            set other = "expanded"
            label start:
            "[nested]|[missing]|[other]|[os_locale]"
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);
        runtime.Start();
        Assert.Equal("[other]||expanded|", runtime.CaptureState().Dialogue!.Text);
    }

    [Fact]
    public void ParticleEmittersAccumulateAndNamedStopSurvivesRestore()
    {
        const string source = """
            label start:
            particle fire at(0.2, 0.3) radius(0.4)
            particle fire
            particle snow intensity(2) wind(0.5)
            "pause"
            particle stop fire
            "after stop"
            particle stop all
            """;
        StoryRuntime original = RuntimeTestHelper.Create(source);
        Assert.Equal(3, original.Start().OfType<ParticleChanged>().Count());
        RuntimeState state = original.CaptureState();
        Assert.Equal(3, state.Particles.Count);
        Assert.Equal(new ParticleEmitterState("fire", "fire", 1, 0, 0.2, 0.3, 0.4), state.Particles[0]);
        Assert.Equal(new ParticleEmitterState("fire", "fire", 1, 0, 0.5, 0.8, 0.1), state.Particles[1]);
        StoryRuntime restored = RuntimeTestHelper.Create(source);
        restored.Restore(state);
        Assert.Equal(original.Advance(), restored.Advance());
        Assert.Equal("snow", Assert.Single(restored.CaptureState().Particles).Name);
        restored.Advance();
        Assert.Empty(restored.CaptureState().Particles);
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Velora.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
