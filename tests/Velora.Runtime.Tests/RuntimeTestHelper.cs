using Velora.Localization;
using Velora.Platform;
using Velora.Runtime.Contracts;
using Velora.Script.Compiler;
using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Runtime.Tests;

internal static class RuntimeTestHelper
{
    public static VeloraProgram Compile(string source, string filePath = "story.vel")
    {
        SyntaxTree syntaxTree = SyntaxTree.ParseText(SourceText.From(source, filePath));
        VeloraCompilation compilation = VeloraCompiler.Compile(SemanticModel.Create(syntaxTree));
        Assert.True(
            compilation.Succeeded,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(static diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Message}")));
        return compilation.Program;
    }

    public static StoryRuntime Create(
        string source,
        TestPlatformInfo? platform = null,
        ITextLocalizer? localizer = null) =>
        new(
            Compile(source),
            platform ?? new TestPlatformInfo(),
            localizer ?? new TestTextLocalizer());

    public static RuntimeValue Variable(this StoryRuntime runtime, string name) =>
        runtime.CaptureState().Variables[name];

    public static double Number(this StoryRuntime runtime, string name) =>
        Assert.IsType<RuntimeValue.NumberValue>(runtime.Variable(name)).Value;

    public static bool Boolean(this StoryRuntime runtime, string name) =>
        Assert.IsType<RuntimeValue.BooleanValue>(runtime.Variable(name)).Value;

    public static string Text(this StoryRuntime runtime, string name) =>
        Assert.IsType<RuntimeValue.StringValue>(runtime.Variable(name)).Value;
}

internal sealed class TestPlatformInfo : IPlatformInfo
{
    public string UserName { get; init; } = "Deterministic User";

    public string PreferredLocale { get; init; } = "en-GB";

    public string LocalTimeText { get; init; } = "09:07";
}

internal sealed class TestTextLocalizer : ITextLocalizer
{
    private readonly Func<string, string, string, string> _translate;

    public TestTextLocalizer(Func<string, string, string, string>? translate = null)
    {
        _translate = translate ?? (static (text, _, _) => text);
    }

    public LocaleCode CurrentLocale => LocaleCode.Parse("en");

    public string Translate(string originalText, string context = "", string speaker = "") =>
        _translate(originalText, context, speaker);

    public string TranslateKey(string key) => key;
}
