using System.Text.Json;
using Velora.Runtime.Contracts;

namespace Velora.Runtime.Tests;

public sealed class LegacyOracleParityTests
{
    [Fact]
    public void StartupPreambleAndInjectedBuiltinsMatchApprovedOracleSemantics()
    {
        const string source = """
            character mira = "Mira" color: #ACCC9D
            set seeded = 2
            set locale = os_locale
            set time = os_time
            config skip_effects = true
            meta lock_save
            label start:
            mira "[sys.os_username] checks [locale] at [time]."
            """;
        var localizer = new TestTextLocalizer(static (text, context, speaker) =>
            context == "say" && speaker == "mira" ? $"localized:{text}" : text);
        StoryRuntime runtime = RuntimeTestHelper.Create(
            source,
            new TestPlatformInfo
            {
                UserName = "Ada",
                PreferredLocale = "uk-UA",
                LocalTimeText = "14:05",
            },
            localizer);

        runtime.Start();

        RuntimeState state = runtime.CaptureState();
        Assert.Equal(RuntimeExecutionStatus.WaitingForAdvance, state.Execution.Status);
        Assert.Equal(2, runtime.Number("seeded"));
        Assert.True(Assert.IsType<RuntimeValue.BooleanValue>(state.Variables["__config.skip_effects"]).Value);
        Assert.Equal("Ada", runtime.Text("sys.os_username"));
        Assert.True(state.IsSaveLocked);
        Assert.Equal("Mira", state.Dialogue?.SpeakerName);
        Assert.Equal("localized:Ada checks uk-UA at 14:05.", state.Dialogue?.Text);
    }

    [Fact]
    public void MixedCoercionTruthinessAndDivisionMatchCommittedOracleFixture()
    {
        const string source = """
            label start:
            set numeric_text_add = "2.5" + 1
            set boolean_add = true + 2
            set number_text_equal = 1 == "1"
            set normalized_number_text_equal = 1 == "1.000000"
            set boolean_text_equal = true == "true"
            set missing_equal_empty = missing == ""
            set divide_zero = 7 / 0
            set unary_bad = -"oops"
            if "false":
                set lowercase_false_branch = 1
            else:
                set lowercase_false_branch = 2
            end
            if "False":
                set uppercase_false_branch = 1
            else:
                set uppercase_false_branch = 2
            end
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);

        runtime.Start();

        Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
        Assert.Equal(3.5, runtime.Number("numeric_text_add"));
        Assert.Equal(3, runtime.Number("boolean_add"));
        Assert.False(runtime.Boolean("number_text_equal"));
        Assert.True(runtime.Boolean("normalized_number_text_equal"));
        Assert.True(runtime.Boolean("boolean_text_equal"));
        Assert.True(runtime.Boolean("missing_equal_empty"));
        Assert.Equal(0, runtime.Number("divide_zero"));
        Assert.Equal(0, runtime.Number("unary_bad"));
        Assert.Equal(2, runtime.Number("lowercase_false_branch"));
        Assert.Equal(1, runtime.Number("uppercase_false_branch"));
        AssertOracleVariables(runtime, "mixed_coercion");
    }

    [Fact]
    public void NumericCoercionConsumesThePrefixWithoutTruncatingOverflow()
    {
        const string source = """
            label start:
            set suffix = "  -12.5e+2suffix" + 0
            set incomplete = "2.5e+oops" + 0
            set dot = ".25tail" + 0
            set overflow = "1e309" + 0
            set giant = "9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999" + 0
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);
        runtime.Start();
        Assert.Equal(-1250, runtime.Number("suffix"));
        Assert.Equal(2.5, runtime.Number("incomplete"));
        Assert.Equal(0.25, runtime.Number("dot"));
        Assert.Equal(0, runtime.Number("overflow"));
        Assert.Equal(0, runtime.Number("giant"));
    }

    [Fact]
    public void MissingJumpWarnsAndContinuesAfterLastWinningStartLabel()
    {
        const string source = """
            label start:
            set reached = 1
            label start:
            set reached = 2
            jump missing_label
            set continued_after_missing_jump = true
            """;
        StoryRuntime runtime = RuntimeTestHelper.Create(source);

        IReadOnlyList<RuntimeEvent> events = runtime.Start();

        Assert.Equal(RuntimeExecutionStatus.Finished, runtime.CaptureState().Execution.Status);
        Assert.Equal(2, runtime.Number("reached"));
        Assert.True(runtime.Boolean("continued_after_missing_jump"));
        RuntimeWarning warning = Assert.Single(events.OfType<RuntimeWarning>());
        Assert.Equal(RuntimeWarningCodes.MissingJumpTarget, warning.Code);
        AssertOracleVariables(runtime, "flow_leniency");
    }

    private static void AssertOracleVariables(StoryRuntime runtime, string scenario)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-runtime-contract.json")));
        foreach (JsonProperty expected in fixture.RootElement.GetProperty(scenario).GetProperty("variables").EnumerateObject())
        {
            switch (expected.Value.ValueKind)
            {
                case JsonValueKind.Number:
                    Assert.Equal(expected.Value.GetDouble(), runtime.Number(expected.Name));
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    Assert.Equal(expected.Value.GetBoolean(), runtime.Boolean(expected.Name));
                    break;
                case JsonValueKind.String:
                    Assert.Equal(expected.Value.GetString(), runtime.Text(expected.Name));
                    break;
                default:
                    Assert.Fail($"Unexpected oracle value kind for {expected.Name}.");
                    break;
            }
        }
    }
}
