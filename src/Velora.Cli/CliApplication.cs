using System.Globalization;
using Velora.Assets;
using Velora.Localization;
using Velora.Platform;
using Velora.ProjectModel;
using Velora.Runtime;
using Velora.Runtime.Contracts;
using Velora.Script.Compiler;
using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Cli;

/// <summary>Portable validation and deterministic simulation using the production compiler/runtime.</summary>
public static class CliApplication
{
    private const int MaximumScriptBytes = 16 * 1024 * 1024;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 1 && args[0] is "--version" or "-v")
        {
            output.WriteLine($"Velora CLI / compiler {VeloraCompiler.CompilerVersion}");
            return 0;
        }

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            output.WriteLine("Velora.Cli validate <game.velproj|story.vel>");
            output.WriteLine("Velora.Cli simulate <game.velproj|story.vel> [--choice N] [--input TEXT] [--max-steps N]");
            output.WriteLine("Simulation is headless: host side effects are reported but never executed.");
            return 0;
        }

        if (args.Length < 2 || args[0] is not ("validate" or "simulate"))
        {
            error.WriteLine("Expected validate or simulate followed by a .velproj or .vel path. Use --help.");
            return 2;
        }

        int choice = 0, maximumSteps = 10000;
        string input = string.Empty;
        for (int index = 2; index < args.Length; index += 2)
        {
            if (args[0] != "simulate" || index + 1 >= args.Length)
            {
                error.WriteLine($"Unexpected or incomplete option '{args[index]}'.");
                return 2;
            }

            switch (args[index])
            {
                case "--choice" when int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out choice):
                    break;
                case "--max-steps" when int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out maximumSteps)
                    && maximumSteps is > 0 and <= 1000000:
                    break;
                case "--input": input = args[index + 1]; break;
                default:
                    error.WriteLine($"Invalid option or value '{args[index]}'.");
                    return 2;
            }
        }

        try
        {
            string path = Path.GetFullPath(args[1]);
            if (string.Equals(Path.GetExtension(path), ".velproj", StringComparison.OrdinalIgnoreCase))
            {
                ProjectLoadResult loaded = VeloraProjectLoader.LoadFile(path);
                foreach (ProjectDiagnostic diagnostic in loaded.Diagnostics)
                {
                    error.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
                }

                if (!loaded.IsSuccess)
                {
                    return 1;
                }

                // The manifest never grants access outside its canonical project root.
                var resolver = new AssetResolver(Path.GetDirectoryName(path)!);
                AssetResolution script = resolver.Resolve(loaded.Project!.EntryScript.Value);
                if (!script.IsSuccess)
                {
                    error.WriteLine(script.Diagnostic!.Message);
                    return 1;
                }

                path = script.FullPath!;
            }
            else if (!string.Equals(Path.GetExtension(path), ".vel", StringComparison.OrdinalIgnoreCase))
            {
                error.WriteLine("Input must be a .vel or .velproj file.");
                return 2;
            }

            using FileStream stream = File.OpenRead(path);
            if (stream.Length > MaximumScriptBytes)
            {
                error.WriteLine("Script exceeds the 16 MiB input limit.");
                return 1;
            }

            // Bound the actual read as well as the initial length: another process
            // may grow a source file after it has been opened.
            using var bounded = new MemoryStream();
            byte[] readBuffer = new byte[8192];
            int read;
            while ((read = stream.Read(readBuffer)) != 0)
            {
                if (bounded.Length + read > MaximumScriptBytes)
                {
                    error.WriteLine("Script exceeds the 16 MiB input limit.");
                    return 1;
                }

                bounded.Write(readBuffer, 0, read);
            }

            bounded.Position = 0;
            SourceText source = SourceText.Read(bounded, path);
            VeloraCompilation compilation = VeloraCompiler.Compile(SemanticModel.Create(SyntaxTree.ParseText(source)));
            foreach (CompilationDiagnostic diagnostic in compilation.Diagnostics)
            {
                LinePosition position = source.GetLinePosition(diagnostic.Source.Start);
                error.WriteLine($"{path}({position.Line + 1},{position.Character + 1}): {diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}");
            }

            if (!compilation.Succeeded)
            {
                return 1;
            }

            output.WriteLine($"Compiled {compilation.Program.Instructions.Length} instructions; SHA-256 {compilation.Program.Fingerprint.ContentHash}");
            if (args[0] == "validate")
            {
                return 0;
            }

            return Simulate(compilation.Program, choice, input, maximumSteps, output, error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine($"Cannot process story: {exception.Message}");
            return 1;
        }
    }

    private static int Simulate(VeloraProgram program, int choice, string input, int maximumSteps,
        TextWriter output, TextWriter error)
    {
        var runtime = new StoryRuntime(program, new SimulationPlatform(), new TextLocalizer(LocaleCode.Parse("en")));
        IReadOnlyList<RuntimeEvent> events = runtime.Start();
        for (int step = 0; step < maximumSteps; step++)
        {
            foreach (RuntimeEvent runtimeEvent in events)
            {
                switch (runtimeEvent)
                {
                    case DialogueStarted dialogue:
                        output.WriteLine($"{dialogue.Dialogue.SpeakerName}: {dialogue.Dialogue.Text}");
                        break;
                    case HostActionRequested hostAction:
                        output.WriteLine($"Host request suppressed: {hostAction.Action}");
                        break;
                    case RuntimeWarning warning:
                        error.WriteLine($"{warning.Code} at instruction {warning.Instruction}: {warning.Message}");
                        if (warning.Code == RuntimeWarningCodes.ExecutionLimitReached)
                        {
                            return 1;
                        }

                        break;
                }
            }

            ExecutionState execution = runtime.CaptureState().Execution;
            switch (execution.Status)
            {
                case RuntimeExecutionStatus.Finished:
                    output.WriteLine("Story finished.");
                    return 0;
                case RuntimeExecutionStatus.WaitingForAdvance:
                    events = runtime.Advance();
                    break;
                case RuntimeExecutionStatus.WaitingForChoice:
                    if (choice >= execution.Choice!.Options.Count)
                    {
                        error.WriteLine($"Choice {choice} does not exist in a menu with {execution.Choice.Options.Count} options.");
                        return 2;
                    }

                    events = runtime.SelectChoice(choice);
                    break;
                case RuntimeExecutionStatus.WaitingForTextInput:
                    events = runtime.SubmitTextInput(input);
                    break;
                case RuntimeExecutionStatus.WaitingForTime:
                    events = runtime.Update(execution.Wait!.Remaining);
                    break;
                default:
                    error.WriteLine($"Unexpected runtime status: {execution.Status}.");
                    return 1;
            }
        }

        error.WriteLine($"Simulation exceeded {maximumSteps} steps.");
        return 1;
    }

    private sealed class SimulationPlatform : IPlatformInfo
    {
        public string UserName => "Reader";

        public string PreferredLocale => "en";

        public string LocalTimeText => "12:00";
    }
}
