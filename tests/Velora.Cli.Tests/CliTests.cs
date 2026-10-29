using System.Globalization;

namespace Velora.Cli.Tests;

public sealed class CliTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    public void InformationalCommandsNeedNoProjectOrWindow(string option)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(0, CliApplication.Run([option], output, error));
        Assert.NotEmpty(output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void SimulationConsumesChoicesTextAndWaitsAndSuppressesHostWrites()
    {
        const string source = """
            label start:
            input "Name" -> name
            if true:
                wait 0.1
                "Welcome [name]"
            end
            menu:
                "Continue":
                    "Selected route"
            end
            meta file_write "never-written.txt" "text"
            """;
        WithScript(source, path =>
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(0, CliApplication.Run(["simulate", path, "--input", "Ada"], output, error));
            Assert.Contains("Welcome Ada", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Selected route", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Host request suppressed: WriteScriptData", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Story finished.", output.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "never-written.txt")));
        });
    }

    [Fact]
    public void InfiniteStoryFailsWithExecutionDiagnostic()
    {
        WithScript("label start:\njump start\n", path =>
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(1, CliApplication.Run(["simulate", path], output, error));
            Assert.Contains("RUN011", error.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MalformedScriptReportsLocationAndFailsValidation()
    {
        WithScript("label start:\nset value =\n", path =>
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(1, CliApplication.Run(["validate", path], output, error));
            Assert.Contains("Error", error.ToString(), StringComparison.Ordinal);
            Assert.Contains(path, error.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void NegativeChoiceIsAnArgumentError()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(2, CliApplication.Run(["simulate", "unused.vel", "--choice", "-1"], output, error));
    }

    private static void WithScript(string source, Action<string> action)
    {
        string path = Path.Combine(Path.GetTempPath(), $"velora-cli-{Guid.NewGuid():N}.vel");
        try
        {
            File.WriteAllText(path, source);
            action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
