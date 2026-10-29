using System.Text;
using Velora.Assets.Tests.TestSupport;
using Velora.ProjectModel;

namespace Velora.Assets.Tests.ProjectModel;

public sealed class VeloraProjectLoaderTests
{
    [Fact]
    public void ParseLoadsVersionedHumanReadableProjectWithoutReflectionFallback()
    {
        const string json = """
            {
              "formatVersion": 1,
              "name": "Station Story",
              "entryScript": "Scripts/chapters/../main.vel",
              "assetsDirectory": "Content/../Assets"
            }
            """;

        var result = VeloraProjectLoader.Parse(Encoding.UTF8.GetBytes(json), "game.velproj");

        Assert.True(result.IsSuccess);
        var project = Assert.IsType<VeloraProject>(result.Project);
        Assert.Equal(1, project.FormatVersion);
        Assert.Equal("Station Story", project.Name);
        Assert.Equal("Scripts/main.vel", project.EntryScript.Value);
        Assert.Equal("Assets", project.AssetsDirectory.Value);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LoadFileAcceptsUtf8BomAndReadsOnlyFromCallerSelectedFile()
    {
        using var temporary = new TemporaryDirectory();
        var filePath = temporary.GetPath("game.velproj");
        var json = """{"formatVersion":1,"name":"Test","entryScript":"Scripts/main.vel","assetsDirectory":"Assets"}""";
        File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = VeloraProjectLoader.LoadFile(filePath);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test", result.Project?.Name);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"formatVersion\":1,\"name\":\"x\",\"entryScript\":\"main.vel\",\"assetsDirectory\":\"Assets\",\"unknown\":true}")]
    public void ParseRejectsMalformedOrUnknownSchema(string json)
    {
        var result = VeloraProjectLoader.Parse(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectDiagnosticIds.MalformedJson, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void ParseRejectsDuplicateProperties()
    {
        const string json = """
            {"formatVersion":1,"name":"first","name":"second","entryScript":"main.vel","assetsDirectory":"Assets"}
            """;

        var result = VeloraProjectLoader.Parse(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectDiagnosticIds.DuplicateProperty, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(2, "Scripts/main.vel", ProjectDiagnosticIds.UnsupportedVersion)]
    [InlineData(1, "../main.vel", ProjectDiagnosticIds.InvalidEntryScript)]
    [InlineData(1, "/tmp/main.vel", ProjectDiagnosticIds.InvalidEntryScript)]
    [InlineData(1, "Scripts/main.txt", ProjectDiagnosticIds.InvalidEntryScript)]
    public void ParseRejectsUnsupportedVersionAndUnsafeEntryPaths(
        int version,
        string entryScript,
        string diagnosticId)
    {
        var json = $$"""
            {"formatVersion":{{version}},"name":"Test","entryScript":"{{entryScript}}","assetsDirectory":"Assets"}
            """;

        var result = VeloraProjectLoader.Parse(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(diagnosticId, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void ParseRejectsOversizedInputBeforeJsonAllocation()
    {
        var input = new byte[VeloraProjectLoader.MaximumFileSize + 1];

        var result = VeloraProjectLoader.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectDiagnosticIds.FileTooLarge, Assert.Single(result.Diagnostics).Id);
    }
}
