using Velora.Runtime.Contracts;

namespace Velora.Script.Compiler.Tests;

public sealed class InstructionLoweringTests
{
    private const string FullProgram = """
        character guide = "Guide" color: #8FB3A2
        config ui.theme = "dark"
        meta set_title "Story"
        screen hud:
            panel at 0.1,0.2 size(0.8,0.6) alpha(0.9):
                text "Notice" color: #FFFFFF action jump finish
                imagebutton "buttons/ok.png" action dismiss
            end
        end
        label start:
        scene "backgrounds/room.png" with dissolve(0.5)
        show guide "sprites/guide.png" at 0.2,0.8 with fade(0.4) zoom(1.2) zorder(3) rotate(-5) flipped
        hide guide with fade(0.2)
        guide "Hello"
        "Narration"
        play bgm "audio/theme.ogg" fadein(1.0) volume(0.7) crossfade(0.2)
        stop bgm fadeout(0.3)
        wait 0.4
        set score = (1 + 2) * 3
        jump finish
        menu:
            "Continue":
                set route = "continue"
            "Wait":
                wait 0.1
        end
        if score >= 3:
            set branch = "then"
        elif score == 2:
            set branch = "elif"
        else:
            set branch = "else"
        end
        tween guide alpha 0.5 duration(0.8) ease_out
        emotion guide happy
        input "Name" -> player.name
        input "Mode" -> mode
            "Story"
            "Normal"
        end
        particle rain intensity(0.4) wind(0.1) at(0.5,0.8) radius(0.12)
        particle stop all
        show_screen hud
        hide_screen hud
        label finish:
        meta main_menu
        """;

    [Fact]
    public void LowersEveryLegacyStatementKindToRuntimeInstruction()
    {
        VeloraProgram program = CompilerTestHelper.Compile(FullProgram).Program;
        Type[] expectedKinds =
        [
            typeof(DefineCharacterInstruction),
            typeof(SetConfigInstruction),
            typeof(ExecuteMetaInstruction),
            typeof(RegisterScreenInstruction),
            typeof(LabelInstruction),
            typeof(ChangeSceneInstruction),
            typeof(ShowSpriteInstruction),
            typeof(HideSpriteInstruction),
            typeof(SayInstruction),
            typeof(PlayAudioInstruction),
            typeof(StopAudioInstruction),
            typeof(WaitInstruction),
            typeof(SetVariableInstruction),
            typeof(JumpInstruction),
            typeof(PresentChoiceInstruction),
            typeof(BranchInstruction),
            typeof(StartTweenInstruction),
            typeof(TriggerEmotionInstruction),
            typeof(RequestInputInstruction),
            typeof(ParticleInstruction),
            typeof(ShowScreenInstruction),
            typeof(HideScreenInstruction),
        ];

        Assert.All(expectedKinds, expected => Assert.Contains(program.Instructions, expected.IsInstanceOfType));
        Assert.Equal(2, program.Instructions.OfType<SayInstruction>().Count());
        Assert.Equal(2, program.Instructions.OfType<RequestInputInstruction>().Count());
        Assert.Equal(2, program.Instructions.OfType<ParticleInstruction>().Count());
    }

    [Fact]
    public void LowersPresentationAndAudioOperandsWithoutBackendTypes()
    {
        VeloraProgram program = CompilerTestHelper.Compile(FullProgram).Program;

        ChangeSceneInstruction scene = Assert.Single(program.Instructions.OfType<ChangeSceneInstruction>());
        Assert.Equal("backgrounds/room.png", scene.ImagePath);
        Assert.Equal(new TransitionDefinition("dissolve", 0.5), scene.Transition);

        ShowSpriteInstruction show = Assert.Single(program.Instructions.OfType<ShowSpriteInstruction>());
        Assert.Equal("guide", show.Target);
        Assert.False(show.IsExpressionShorthand);
        Assert.Equal(SpritePositionKind.Coordinates, show.Position?.Kind);
        Assert.Equal(0.2, show.Position?.X);
        Assert.Equal(0.8, show.Position?.Y);
        Assert.Equal(["zoom", "zorder", "rotate", "flipped"], show.Modifiers.Select(static value => value.Name));

        PlayAudioInstruction play = Assert.Single(program.Instructions.OfType<PlayAudioInstruction>());
        Assert.Equal("bgm", play.Channel);
        Assert.Equal(["fadein", "volume", "crossfade"], play.Modifiers.Select(static value => value.Name));
    }

    [Fact]
    public void PreservesSpriteExpressionShorthandAndPresetPosition()
    {
        VeloraProgram program = CompilerTestHelper.Compile("show mira happy winter at left closeup").Program;
        ShowSpriteInstruction show = Assert.Single(program.Instructions.OfType<ShowSpriteInstruction>());

        Assert.True(show.IsExpressionShorthand);
        Assert.Equal("happy_winter", show.ImagePath);
        Assert.Equal(SpritePositionKind.Preset, show.Position?.Kind);
        Assert.Equal("left", show.Position?.Preset);
        Assert.Equal("closeup", Assert.Single(show.Modifiers).Name);
    }

    [Fact]
    public void LowersScreenHierarchyPropertiesAndResolvedActions()
    {
        VeloraProgram program = CompilerTestHelper.Compile(FullProgram).Program;
        RegisterScreenInstruction register = Assert.Single(program.Instructions.OfType<RegisterScreenInstruction>());
        ScreenElementDefinition panel = Assert.Single(register.Screen.Elements);
        Assert.Equal(ScreenElementKind.Panel, panel.Kind);
        Assert.Equal(3, panel.Properties.Length);
        Assert.Equal(2, panel.Children.Length);

        ScreenElementDefinition text = panel.Children[0];
        Assert.Equal("Notice", text.Text);
        Assert.Equal(ScreenActionKind.Jump, text.Action?.Kind);
        Assert.Equal("finish", text.Action?.Value);
        Assert.IsType<LabelInstruction>(program.GetInstruction(text.Action!.TargetInstructionId!.Value));

        ScreenElementDefinition image = panel.Children[1];
        Assert.Equal("buttons/ok.png", image.ImagePath);
        Assert.Equal(ScreenActionKind.Command, image.Action?.Kind);
        Assert.Equal("dismiss", image.Action?.Value);
        Assert.Null(image.Action?.TargetInstructionId);
    }

    [Fact]
    public void LowersRuntimeCommandOperandsToTypedIr()
    {
        VeloraProgram program = CompilerTestHelper.Compile(FullProgram).Program;

        SetConfigInstruction config = Assert.Single(program.Instructions.OfType<SetConfigInstruction>());
        Assert.Equal("ui.theme", config.Key);
        Assert.Equal("dark", Assert.IsType<StringExpression>(config.Value).Value);

        ExecuteMetaInstruction title = program.Instructions.OfType<ExecuteMetaInstruction>()
            .First(static instruction => instruction.Action == "set_title");
        Assert.Equal(IrArgumentKind.Text, Assert.Single(title.Arguments).Kind);

        StartTweenInstruction tween = Assert.Single(program.Instructions.OfType<StartTweenInstruction>());
        Assert.Equal("guide", tween.Target);
        Assert.Equal("alpha", tween.Property);
        Assert.Equal(0.8, tween.DurationSeconds);
        Assert.Equal("ease_out", tween.Easing);

        RequestInputInstruction selection = program.Instructions.OfType<RequestInputInstruction>()
            .Single(static instruction => instruction.Options.Length != 0);
        Assert.Equal("mode", selection.TargetVariable);
        Assert.Equal(["Story", "Normal"], selection.Options);

        ParticleInstruction stop = program.Instructions.OfType<ParticleInstruction>()
            .Single(static instruction => instruction.Preset == "stop");
        Assert.True(stop.StopAll);
        Assert.Equal("all", stop.Name);
    }

    [Fact]
    public void UsesLegacyDefaultsForOptionalOperands()
    {
        const string source = """
            character guide = "Guide"
            scene "bg.png" with fade
            tween guide x 1
            play "sound.ogg"
            stop
            """;

        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        Assert.Equal("#FFFFFF", Assert.Single(program.Instructions.OfType<DefineCharacterInstruction>()).Color);
        Assert.Equal(1, Assert.Single(program.Instructions.OfType<ChangeSceneInstruction>()).Transition?.Duration);
        StartTweenInstruction tween = Assert.Single(program.Instructions.OfType<StartTweenInstruction>());
        Assert.Equal(1, tween.DurationSeconds);
        Assert.Equal("ease_out", tween.Easing);
        Assert.Equal("sfx", Assert.Single(program.Instructions.OfType<PlayAudioInstruction>()).Channel);
        Assert.Equal("bgm", Assert.Single(program.Instructions.OfType<StopAudioInstruction>()).Channel);
    }

    [Fact]
    public void InstructionIdsAreDenseDeterministicOrdinals()
    {
        VeloraProgram program = CompilerTestHelper.Compile(FullProgram).Program;

        Assert.Equal(
            Enumerable.Range(0, program.Instructions.Length),
            program.Instructions.Select(static instruction => instruction.Id.Ordinal));
        Assert.All(program.Instructions, instruction =>
            Assert.Same(instruction, program.GetInstruction(instruction.Id)));
    }
}
