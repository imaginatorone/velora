using System.Reflection;
using System.Globalization;
using Velora.Runtime.Contracts;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Compiler.Tests;

public sealed class ProgramContractTests
{
    [Fact]
    public void EmptySourceProducesValidEmptyProgram()
    {
        VeloraCompilation compilation = CompilerTestHelper.Compile(string.Empty);

        Assert.True(compilation.Succeeded);
        Assert.Empty(compilation.Diagnostics);
        Assert.Empty(compilation.Program.Instructions);
        Assert.Empty(compilation.Program.SourceMap.Entries);
        Assert.Empty(compilation.Program.InitializationInstructionIds);
        Assert.Null(compilation.Program.EntryInstructionId);
        Assert.Null(compilation.Program.EntryLabel);
        Assert.Equal(64, compilation.Program.Fingerprint.ContentHash.Length);
    }

    [Fact]
    public void EveryInstructionHasExactDenseSourceMapEntry()
    {
        const string source = """
            character guide = "Guide"
            label start:
                guide "Hello"
                wait 0.5
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source, "mapped.vel").Program;

        Assert.Equal(program.Instructions.Length, program.SourceMap.Entries.Length);
        foreach (VeloraInstruction instruction in program.Instructions)
        {
            SourceMapEntry entry = program.SourceMap.GetEntry(instruction.Id);
            Assert.Equal(instruction.Source, entry.Source);
            Assert.Equal(instruction.ContainingLabel, entry.ContainingLabel);
            Assert.Equal("mapped.vel", entry.Source.FilePath);
            Assert.Equal(source.Substring(entry.Source.Start, entry.Source.Length).Trim(),
                source.Substring(instruction.Source.Start, instruction.Source.Length).Trim());
        }

        LabelInstruction label = Assert.Single(program.Instructions.OfType<LabelInstruction>());
        DefineCharacterInstruction character = Assert.Single(program.Instructions.OfType<DefineCharacterInstruction>());
        SayInstruction say = Assert.Single(program.Instructions.OfType<SayInstruction>());
        WaitInstruction wait = Assert.Single(program.Instructions.OfType<WaitInstruction>());
        Assert.Equal(source.IndexOf("character", StringComparison.Ordinal), character.Source.Start);
        Assert.Equal("character guide = \"Guide\"".Length, character.Source.Length);
        Assert.Equal(source.IndexOf("label", StringComparison.Ordinal), label.Source.Start);
        Assert.Equal("label start:".Length, label.Source.Length);
        Assert.Equal(source.IndexOf("guide \"Hello\"", StringComparison.Ordinal), say.Source.Start);
        Assert.Equal("guide \"Hello\"".Length, say.Source.Length);
        Assert.Equal(source.IndexOf("wait", StringComparison.Ordinal), wait.Source.Start);
        Assert.Equal("wait 0.5".Length, wait.Source.Length);
        Assert.Equal("start", label.ContainingLabel);
        Assert.All(
            program.Instructions.Where(static instruction => instruction is SayInstruction or WaitInstruction),
            static instruction => Assert.Equal("start", instruction.ContainingLabel));
    }

    [Fact]
    public void SourceSpansUseUtf16CodeUnits()
    {
        const string source = "\"🌙 night\"\nwait 1";
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        WaitInstruction wait = Assert.Single(program.Instructions.OfType<WaitInstruction>());

        Assert.Equal(source.IndexOf("wait", StringComparison.Ordinal), wait.Source.Start);
        Assert.Equal("wait 1".Length, wait.Source.Length);
    }

    [Fact]
    public void FingerprintIsStableAcrossRepeatedCompilationAndFormattingOnlyChanges()
    {
        const string compact = "set value = 1\nlabel start:\n\"Hello\"";
        const string formatted = """
            -- formatting is not executable
              set value = 1

            label start:
                "Hello" -- trailing comment
            """;

        ProgramFingerprint first = CompilerTestHelper.Compile(compact).Program.Fingerprint;
        ProgramFingerprint second = CompilerTestHelper.Compile(compact).Program.Fingerprint;
        ProgramFingerprint formattedFingerprint = CompilerTestHelper.Compile(formatted).Program.Fingerprint;

        Assert.Equal(first, second);
        Assert.Equal(first, formattedFingerprint);
        Assert.Equal(VeloraCompiler.CompilerVersion, first.CompilerVersion);
        Assert.Equal(ProgramFingerprint.ContentHashLength, first.ContentHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", first.ContentHash);
    }

    [Fact]
    public void FingerprintChangesForValuesOrderingAndControlFlow()
    {
        string baseline = CompilerTestHelper.Compile("set value = 1\nlabel end:").Program.Fingerprint.ContentHash;

        Assert.NotEqual(baseline, CompilerTestHelper.Compile("set value = 2\nlabel end:").Program.Fingerprint.ContentHash);
        Assert.NotEqual(baseline, CompilerTestHelper.Compile("label end:\nset value = 1").Program.Fingerprint.ContentHash);
        Assert.NotEqual(baseline, CompilerTestHelper.Compile("set value = 1\nlabel other:").Program.Fingerprint.ContentHash);
    }

    [Fact]
    public void CanonicalFingerprintHasLockedGoldenValue()
    {
        const string source = """
            set visits = 0
            label start:
            if visits == 0:
                "Hello"
            else:
                jump start
            end
            """;

        Assert.Equal(
            "67d6e5e11ce83bf5f399f8f56a81cd377056a9f30181a49d39b49bcff278c84a",
            CompilerTestHelper.Compile(source).Program.Fingerprint.ContentHash);
    }

    [Fact]
    public void GlobalDefinitionsAreDeterministicAndUseEffectiveWrites()
    {
        const string source = """
            set zebra = 1
            character zed = "Zed"
            set alpha = 1
            set alpha = 2
            screen overlay:
            end
            label start:
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        GlobalDefinition[] explicitDefinitions =
        [
            .. program.Definitions.Where(static definition => !definition.IsImplicit),
        ];

        Assert.Equal(
            [
                (GlobalDefinitionKind.Label, "start"),
                (GlobalDefinitionKind.Character, "zed"),
                (GlobalDefinitionKind.Screen, "overlay"),
                (GlobalDefinitionKind.Variable, "alpha"),
                (GlobalDefinitionKind.Variable, "zebra"),
            ],
            explicitDefinitions.Select(static definition => (definition.Kind, definition.Name)));
        GlobalDefinition alpha = explicitDefinitions.Single(static definition =>
            definition.Kind == GlobalDefinitionKind.Variable && definition.Name == "alpha");
        Assert.Equal(
            "alpha",
            Assert.IsType<SetVariableInstruction>(program.GetInstruction(alpha.EffectiveInstructionId!.Value)).Name);
        Assert.Contains(program.Definitions, static definition => definition.Name == "os_username" && definition.IsImplicit);
    }

    [Fact]
    public void MalformedSourceProducesRecoverableProgramAndPreservesDiagnostics()
    {
        const string source = """
            set value =
            ??? bad
            if :
                wait nope
            """;

        VeloraCompilation compilation = CompilerTestHelper.Compile(source, "broken.vel");

        Assert.False(compilation.Succeeded);
        Assert.NotEmpty(compilation.Program.Instructions);
        Assert.Contains(compilation.Diagnostics, static diagnostic => diagnostic.Id.StartsWith("PAR", StringComparison.Ordinal));
        Assert.Contains(compilation.Diagnostics, static diagnostic => diagnostic.Id == CompilerDiagnosticIds.InvalidExpression);
        Assert.Contains(compilation.Diagnostics, static diagnostic => diagnostic.Id == CompilerDiagnosticIds.SkippedInvalidStatement);
        Assert.All(compilation.Diagnostics, static diagnostic => Assert.Equal("broken.vel", diagnostic.Source.FilePath));
    }

    [Fact]
    public void EmptyMenuIsDiagnosedAndSkippedToKeepRecoverableIrExecutable()
    {
        const string source = """
            menu:
            end
            set continued = true
            """;

        VeloraCompilation compilation = CompilerTestHelper.Compile(source);

        Assert.False(compilation.Succeeded);
        Assert.Contains(compilation.Diagnostics, static diagnostic =>
            diagnostic.Id == CompilerDiagnosticIds.EmptyMenu);
        Assert.Empty(compilation.Program.Instructions.OfType<PresentChoiceInstruction>());
        SetVariableInstruction continued = Assert.Single(
            compilation.Program.Instructions.OfType<SetVariableInstruction>());
        Assert.Equal(continued.Id, compilation.Program.EntryInstructionId);
    }

    [Fact]
    public void BundledExampleCompilesWithoutDiagnosticsAndHasStartEntry()
    {
        string root = CompilerTestHelper.FindRepositoryRoot();
        string path = Path.Combine(root, "assets", "scripts", "example.vel");
        SyntaxTree syntaxTree = SyntaxTree.ParseText(SourceText.ReadFrom(path));

        VeloraCompilation compilation = VeloraCompiler.Compile(
            Velora.Script.Semantics.SemanticModel.Create(syntaxTree));

        Assert.True(compilation.Succeeded);
        Assert.Empty(compilation.Diagnostics);
        Assert.NotEmpty(compilation.Program.Instructions);
        Assert.NotEmpty(compilation.Program.InitializationInstructionIds);
        Assert.Equal("start", compilation.Program.EntryLabel);
        Assert.IsType<ParticleInstruction>(
            compilation.Program.GetInstruction(compilation.Program.EntryInstructionId!.Value));
    }

    [Fact]
    public void KitchenSinkCompilesAndLocksCorrectedNestedContinuationSemantics()
    {
        string root = CompilerTestHelper.FindRepositoryRoot();
        string path = Path.Combine(root, "samples", "KitchenSink", "Scripts", "main.vel");
        SyntaxTree syntaxTree = SyntaxTree.ParseText(SourceText.ReadFrom(path));

        VeloraCompilation compilation = VeloraCompiler.Compile(
            Velora.Script.Semantics.SemanticModel.Create(syntaxTree));

        Assert.True(compilation.Succeeded);
        Assert.Empty(compilation.Diagnostics);
        Assert.NotEmpty(compilation.Program.InitializationInstructionIds);
        Assert.Equal("start", compilation.Program.EntryLabel);
        Assert.IsType<ChangeSceneInstruction>(
            compilation.Program.GetInstruction(compilation.Program.EntryInstructionId!.Value));

        SetVariableInstruction choiceRemainder = TrueAssignment(
            compilation.Program,
            "choice_after_dialogue");
        SetVariableInstruction dialogueRemainder = TrueAssignment(
            compilation.Program,
            "nested_after_dialogue");
        SetVariableInstruction waitRemainder = TrueAssignment(
            compilation.Program,
            "nested_after_wait");

        Assert.Equal(
            choiceRemainder.Id,
            compilation.Program.Instructions.OfType<SayInstruction>().Single(static instruction =>
                instruction.Text == "The selected choice pauses before its remaining body.")
                .ContinuationInstructionId);
        Assert.Equal(
            dialogueRemainder.Id,
            compilation.Program.Instructions.OfType<SayInstruction>().Single(static instruction =>
                instruction.Text == "A nested dialogue now owns an explicit continuation.")
                .ContinuationInstructionId);
        Assert.Equal(
            waitRemainder.Id,
            compilation.Program.Instructions.OfType<WaitInstruction>().Single(static instruction =>
                instruction.DurationSeconds == 0.05)
                .ContinuationInstructionId);
    }

    [Fact]
    public void GoldenInstructionLayoutIsStable()
    {
        const string source = """
            label start:
            if ready:
                "Yes"
            else:
                "No"
            end
            menu:
                "Again":
                    jump start
            end
            meta main_menu
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;
        string[] layout =
        [
            .. program.Instructions.Select(static instruction =>
                $"{instruction.Id.Ordinal}:{instruction.GetType().Name}->{instruction.ContinuationInstructionId?.Ordinal.ToString(CultureInfo.InvariantCulture) ?? "end"}@{instruction.ContainingLabel ?? "-"}"),
        ];

        Assert.Equal(
            [
                "0:LabelInstruction->1@start",
                "1:BranchInstruction->4@start",
                "2:SayInstruction->4@start",
                "3:SayInstruction->4@start",
                "4:PresentChoiceInstruction->6@start",
                "5:JumpInstruction->6@start",
                "6:ExecuteMetaInstruction->end@start",
            ],
            layout);
    }

    [Fact]
    public void PublicIrSurfaceDoesNotLeakSyntaxNodes()
    {
        Assembly assembly = typeof(VeloraProgram).Assembly;
        Type syntaxNode = typeof(SyntaxNode);
        Type[] irTypes =
        [
            .. assembly.GetExportedTypes().Where(static type =>
                type.Namespace == typeof(VeloraProgram).Namespace && type != typeof(VeloraCompiler)),
        ];

        foreach (Type type in irTypes)
        {
            AssertNoSyntaxType(type.BaseType, syntaxNode, $"{type.FullName} base type");
            foreach (Type implementedInterface in type.GetInterfaces())
            {
                AssertNoSyntaxType(implementedInterface, syntaxNode, $"{type.FullName} interface");
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
            {
                AssertNoSyntaxType(field.FieldType, syntaxNode, $"{type.FullName}.{field.Name}");
            }

            foreach (PropertyInfo property in type.GetProperties(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
            {
                AssertNoSyntaxType(property.PropertyType, syntaxNode, $"{type.FullName}.{property.Name}");
                foreach (ParameterInfo parameter in property.GetIndexParameters())
                {
                    AssertNoSyntaxType(parameter.ParameterType, syntaxNode,
                        $"{type.FullName}.{property.Name}[{parameter.Name}]");
                }
            }

            foreach (EventInfo eventInfo in type.GetEvents(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
            {
                AssertNoSyntaxType(eventInfo.EventHandlerType, syntaxNode, $"{type.FullName}.{eventInfo.Name}");
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                AssertParameters(constructor.GetParameters(), syntaxNode, $"{type.FullName} constructor");
            }

            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                AssertNoSyntaxType(method.ReturnType, syntaxNode, $"{type.FullName}.{method.Name} return");
                AssertParameters(method.GetParameters(), syntaxNode, $"{type.FullName}.{method.Name}");
                foreach (Type genericArgument in method.GetGenericArguments())
                {
                    foreach (Type constraint in genericArgument.GetGenericParameterConstraints())
                    {
                        AssertNoSyntaxType(constraint, syntaxNode,
                            $"{type.FullName}.{method.Name} generic constraint");
                    }
                }
            }
        }
    }

    private static void AssertParameters(
        IEnumerable<ParameterInfo> parameters,
        Type syntaxNode,
        string owner)
    {
        foreach (ParameterInfo parameter in parameters)
        {
            AssertNoSyntaxType(parameter.ParameterType, syntaxNode, $"{owner} parameter {parameter.Name}");
        }
    }

    private static void AssertNoSyntaxType(Type? candidate, Type syntaxNode, string owner)
    {
        if (candidate is not null)
        {
            Assert.False(ContainsType(candidate, syntaxNode), $"{owner} leaks {candidate}.");
        }
    }

    private static bool ContainsType(Type candidate, Type forbidden)
    {
        if (forbidden.IsAssignableFrom(candidate))
        {
            return true;
        }

        if (candidate.IsArray)
        {
            return ContainsType(candidate.GetElementType()!, forbidden);
        }

        if (candidate.IsByRef || candidate.IsPointer)
        {
            return ContainsType(candidate.GetElementType()!, forbidden);
        }

        return candidate.IsGenericType && candidate.GetGenericArguments().Any(argument =>
            ContainsType(argument, forbidden));
    }

    private static SetVariableInstruction TrueAssignment(VeloraProgram program, string name) =>
        program.Instructions.OfType<SetVariableInstruction>().Single(instruction =>
            instruction.Name == name && instruction.Value is BooleanExpression { Value: true });
}
