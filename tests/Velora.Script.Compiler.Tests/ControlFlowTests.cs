namespace Velora.Script.Compiler.Tests;

public sealed class ControlFlowTests
{
    [Fact]
    public void BranchBodiesResumeInsideBranchBeforeOuterContinuation()
    {
        const string source = """
            label start:
            if ready:
                guide "Pause first"
                wait 0.2
                set completed = true
            else:
                "Fallback"
            end
            meta main_menu
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        BranchInstruction branch = Assert.Single(program.Instructions.OfType<BranchInstruction>());
        ExecuteMetaInstruction outer = Assert.Single(program.Instructions.OfType<ExecuteMetaInstruction>());
        SayInstruction firstPause = program.Instructions.OfType<SayInstruction>().First();
        WaitInstruction secondPause = Assert.Single(program.Instructions.OfType<WaitInstruction>());
        SetVariableInstruction remainingBody = Assert.Single(program.Instructions.OfType<SetVariableInstruction>());

        Assert.Equal(firstPause.Id, Assert.Single(branch.Branches).TargetInstructionId);
        Assert.Equal(secondPause.Id, firstPause.ContinuationInstructionId);
        Assert.Equal(remainingBody.Id, secondPause.ContinuationInstructionId);
        Assert.Equal(outer.Id, remainingBody.ContinuationInstructionId);
        Assert.Equal(outer.Id, branch.ContinuationInstructionId);
    }

    [Fact]
    public void ChoiceBodiesResumeInsideChosenBodyBeforeOuterContinuation()
    {
        const string source = """
            menu:
                "Talk":
                    "First"
                    set talked = true
                "Wait":
                    wait 1
            end
            set after = true
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        PresentChoiceInstruction menu = Assert.Single(program.Instructions.OfType<PresentChoiceInstruction>());
        SetVariableInstruction after = program.Instructions.OfType<SetVariableInstruction>()
            .Single(static instruction => instruction.Name == "after");
        SayInstruction first = Assert.Single(program.Instructions.OfType<SayInstruction>());
        SetVariableInstruction talked = program.Instructions.OfType<SetVariableInstruction>()
            .Single(static instruction => instruction.Name == "talked");
        WaitInstruction wait = Assert.Single(program.Instructions.OfType<WaitInstruction>());

        Assert.Equal(first.Id, menu.Choices[0].TargetInstructionId);
        Assert.Equal(talked.Id, first.ContinuationInstructionId);
        Assert.Equal(after.Id, talked.ContinuationInstructionId);
        Assert.Equal(wait.Id, menu.Choices[1].TargetInstructionId);
        Assert.Equal(after.Id, wait.ContinuationInstructionId);
        Assert.Equal(after.Id, menu.ContinuationInstructionId);
    }

    [Fact]
    public void NestedMenuAndConditionalHaveExplicitJoinTargets()
    {
        const string source = """
            if outer:
                menu:
                    "A":
                        if inner:
                            set value = 1
                        end
                    "B":
                end
            end
            set done = true
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        BranchInstruction[] branches = [.. program.Instructions.OfType<BranchInstruction>()];
        PresentChoiceInstruction menu = Assert.Single(program.Instructions.OfType<PresentChoiceInstruction>());
        SetVariableInstruction done = program.Instructions.OfType<SetVariableInstruction>()
            .Single(static instruction => instruction.Name == "done");

        Assert.Equal(2, branches.Length);
        Assert.Equal(menu.Id, branches[0].Branches[0].TargetInstructionId);
        Assert.Equal(branches[1].Id, menu.Choices[0].TargetInstructionId);
        Assert.Equal(done.Id, menu.Choices[1].TargetInstructionId);
        Assert.Equal(done.Id, branches[0].ContinuationInstructionId);
        Assert.Equal(done.Id, branches[1].ContinuationInstructionId);
        Assert.Equal(done.Id, menu.ContinuationInstructionId);
    }

    [Fact]
    public void ForwardJumpAndScreenActionResolveToEffectiveLabelInstruction()
    {
        const string source = """
            jump future
            screen buttons:
                imagebutton "go.png" action jump future
            end
            label future:
            "Done"
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        LabelInstruction label = Assert.Single(program.Instructions.OfType<LabelInstruction>());
        JumpInstruction jump = Assert.Single(program.Instructions.OfType<JumpInstruction>());
        RegisterScreenInstruction screen = Assert.Single(program.Instructions.OfType<RegisterScreenInstruction>());

        Assert.Equal(label.Id, jump.TargetInstructionId);
        Assert.Equal(label.Id, Assert.Single(screen.Screen.Elements).Action?.TargetInstructionId);
    }

    [Fact]
    public void DuplicateLabelsUseLastDefinitionAsJumpTarget()
    {
        const string source = """
            label destination:
            "First"
            jump destination
            label destination:
            "Second"
            """;
        VeloraCompilation compilation = CompilerTestHelper.Compile(source);
        LabelInstruction[] labels = [.. compilation.Program.Instructions.OfType<LabelInstruction>()];
        JumpInstruction jump = Assert.Single(compilation.Program.Instructions.OfType<JumpInstruction>());

        Assert.Equal(2, labels.Length);
        Assert.False(labels[0].IsJumpTarget);
        Assert.True(labels[1].IsJumpTarget);
        Assert.Equal(labels[1].Id, jump.TargetInstructionId);
        Assert.Contains(compilation.Diagnostics, static diagnostic => diagnostic.Id == "VEL2001");
    }

    [Fact]
    public void MissingJumpTargetRetainsFallthroughContinuation()
    {
        const string source = """
            jump absent
            "Still runs"
            """;
        VeloraCompilation compilation = CompilerTestHelper.Compile(source);
        JumpInstruction jump = Assert.Single(compilation.Program.Instructions.OfType<JumpInstruction>());
        SayInstruction next = Assert.Single(compilation.Program.Instructions.OfType<SayInstruction>());

        Assert.Null(jump.TargetInstructionId);
        Assert.Equal(next.Id, jump.ContinuationInstructionId);
        Assert.Contains(compilation.Diagnostics, static diagnostic => diagnostic.Id == "VEL2004");
    }

    [Fact]
    public void EffectiveStartProducesRestrictedSourceOrderInitializationPreamble()
    {
        const string source = """
            character guide = "Guide"
            "Not initialization"
            set value = 1
            screen hud:
            end
            meta set_title "Title"
            config locale = "en"
            particle rain
            label start:
            set after = true
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        VeloraInstruction[] initialization =
        [
            .. program.InitializationInstructionIds.Select(program.GetInstruction),
        ];

        Assert.Collection(
            initialization,
            static instruction => Assert.IsType<DefineCharacterInstruction>(instruction),
            static instruction => Assert.IsType<SetVariableInstruction>(instruction),
            static instruction => Assert.IsType<RegisterScreenInstruction>(instruction),
            static instruction => Assert.IsType<ExecuteMetaInstruction>(instruction),
            static instruction => Assert.IsType<SetConfigInstruction>(instruction));
        Assert.Equal("start", program.EntryLabel);
        Assert.Equal(
            "after",
            Assert.IsType<SetVariableInstruction>(program.GetInstruction(program.EntryInstructionId!.Value)).Name);
    }

    [Fact]
    public void LastStartLabelDeterminesPreambleAndEntry()
    {
        const string source = """
            set first = 1
            label start:
            set between = 2
            label start:
            set after = 3
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;

        Assert.Equal(2, program.InitializationInstructionIds.Length);
        Assert.Equal(
            ["first", "between"],
            program.InitializationInstructionIds
                .Select(program.GetInstruction)
                .Cast<SetVariableInstruction>()
                .Select(static instruction => instruction.Name));
        Assert.Equal(
            "after",
            Assert.IsType<SetVariableInstruction>(program.GetInstruction(program.EntryInstructionId!.Value)).Name);
    }

    [Fact]
    public void ProgramWithoutStartBeginsAtFirstInstructionAndHasNoPreamble()
    {
        VeloraProgram program = CompilerTestHelper.Compile("set value = 1").Program;

        Assert.Empty(program.InitializationInstructionIds);
        Assert.Null(program.EntryLabel);
        Assert.Equal(program.Instructions[0].Id, program.EntryInstructionId);
    }
}
