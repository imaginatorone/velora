using Velora.ProjectModel;

namespace Velora.Assets.Tests.ProjectModel;

public sealed class KitchenSinkProjectTests
{
    [Fact]
    public void KitchenSinkManifestAndSourceOwnedAssetsFormAValidProject()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "samples", "KitchenSink");
        string manifestPath = Path.Combine(projectRoot, "game.velproj");

        ProjectLoadResult result = VeloraProjectLoader.LoadFile(manifestPath);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        VeloraProject project = Assert.IsType<VeloraProject>(result.Project);
        Assert.Equal("Velora Kitchen Sink", project.Name);
        Assert.Equal("Scripts/main.vel", project.EntryScript.Value);
        Assert.True(File.Exists(Path.Combine(projectRoot, project.EntryScript.Value)));

        var resolver = new AssetResolver(Path.Combine(projectRoot, project.AssetsDirectory.Value));
        AssetDatabase database = AssetDatabase.Scan(resolver);

        Assert.Empty(database.Diagnostics);
        Assert.Equal(
            [
                "backgrounds/grid.svg",
                "sprites/guide.svg",
                "sprites/operator.svg",
                "ui/continue.svg",
            ],
            database.Entries.Select(static entry => entry.Id.Value));
        Assert.All(database.Entries, static entry => Assert.True(entry.Length > 0));

        foreach (AssetEntry entry in database.Entries)
        {
            AssetOpenResult opened = resolver.OpenRead(entry.Id.Value);
            Assert.True(opened.IsSuccess);
            using Stream stream = Assert.IsAssignableFrom<Stream>(opened.Stream);
            Assert.True(stream.CanRead);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "samples", "KitchenSink", "game.velproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Velora repository root.");
    }
}
