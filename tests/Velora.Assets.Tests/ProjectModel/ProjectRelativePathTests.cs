using Velora.ProjectModel;

namespace Velora.Assets.Tests.ProjectModel;

public sealed class ProjectRelativePathTests
{
    [Fact]
    public void TryCreateNormalizesBenignSegmentsAndSeparators()
    {
        var success = ProjectRelativePath.TryCreate(
            @"Scripts\chapters\..\main.vel",
            out var path,
            out var error);

        Assert.True(success);
        Assert.Equal(ProjectPathError.None, error);
        Assert.Equal("Scripts/main.vel", path.Value);
    }

    [Theory]
    [InlineData("../game.vel")]
    [InlineData("Scripts/../../game.vel")]
    [InlineData("/tmp/game.vel")]
    [InlineData(@"C:\game.vel")]
    [InlineData(@"\\server\share\game.vel")]
    public void TryCreateRejectsPortableRootEscapes(string candidate)
    {
        Assert.False(ProjectRelativePath.TryCreate(candidate, out _, out _));
    }
}
