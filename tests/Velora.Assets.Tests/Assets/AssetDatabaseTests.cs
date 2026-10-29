using Velora.Assets.Diagnostics;
using Velora.Assets.Tests.TestSupport;

namespace Velora.Assets.Tests.Assets;

public sealed class AssetDatabaseTests
{
    [Fact]
    public void ScanProducesDeterministicCaseSensitiveEntriesAndLookupDiagnostics()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        temporary.WriteText("Assets/z-last.txt", "z");
        temporary.WriteText("Assets/Images/Hero.PNG", "hero");

        var database = AssetDatabase.Scan(new AssetResolver(root));

        Assert.Collection(
            database.Entries,
            static entry => Assert.Equal("Images/Hero.PNG", entry.Id.Value),
            static entry => Assert.Equal("z-last.txt", entry.Id.Value));
        Assert.True(database.Lookup("Images/Hero.PNG").IsSuccess);
        var mismatch = database.Lookup("images/hero.png");
        Assert.Equal(AssetDiagnosticIds.CaseMismatch, mismatch.Diagnostic?.Id);
        Assert.Equal("Images/Hero.PNG", mismatch.Diagnostic?.SuggestedLogicalPath);
        Assert.Equal(
            AssetDiagnosticIds.MissingAsset,
            database.Lookup("missing.png").Diagnostic?.Id);
    }

    [Fact]
    public void ScanMountShadowsPrimaryAssetAndReportsDuplicateLogicalId()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var mountRoot = temporary.CreateDirectory("DlcMount");
        temporary.WriteText("Assets/dlc/chapter.txt", "primary");
        temporary.WriteText("DlcMount/chapter.txt", "mounted");
        var resolver = new AssetResolver(
            root,
            new[] { AssetMount.Create("dlc", mountRoot) });

        var database = AssetDatabase.Scan(resolver);
        var entry = Assert.Single(database.Entries);

        Assert.Equal(AssetOrigin.Mount, entry.Origin);
        Assert.Contains(
            database.Diagnostics,
            static diagnostic => diagnostic.Id == AssetDiagnosticIds.DuplicateLogicalId);
    }

    [Fact]
    public void ScanDiagnosesCaseCollisionsWhenFilesystemCanRepresentThem()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        temporary.WriteText("Assets/Hero.png", "upper");
        temporary.WriteText("Assets/hero.png", "lower");
        if (Directory.EnumerateFiles(root).Count() < 2)
        {
            return;
        }

        var database = AssetDatabase.Scan(new AssetResolver(root));

        Assert.Contains(
            database.Diagnostics,
            static diagnostic => diagnostic.Id == AssetDiagnosticIds.CaseCollision);
    }

    [Fact]
    public void ScanRejectsEscapingFileLinkAndSkipsDirectoryAliases()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var internalDirectory = temporary.CreateDirectory("Assets/real");
        var outsideFile = temporary.WriteText("outside/secret.txt", "secret");
        temporary.WriteText("Assets/real/safe.txt", "safe");
        var fileLinkCreated = temporary.TryCreateFileLink("Assets/escape.txt", outsideFile);
        var directoryLinkCreated = temporary.TryCreateDirectoryLink("Assets/alias", internalDirectory);
        Assert.True(
            (fileLinkCreated && directoryLinkCreated) || OperatingSystem.IsWindows(),
            "Symbolic links must be available on Unix test hosts.");
        if (!fileLinkCreated || !directoryLinkCreated)
        {
            return;
        }

        var database = AssetDatabase.Scan(new AssetResolver(root));

        Assert.DoesNotContain(database.Entries, static entry => entry.Id.Value == "escape.txt");
        Assert.DoesNotContain(database.Entries, static entry => entry.Id.Value.StartsWith("alias/", StringComparison.Ordinal));
        Assert.Contains(
            database.Diagnostics,
            static diagnostic => diagnostic.Id == AssetDiagnosticIds.ContainmentViolation);
        Assert.Contains(
            database.Diagnostics,
            static diagnostic => diagnostic.Id == AssetDiagnosticIds.DirectoryAliasSkipped);
    }

    [Fact]
    public void ScanDiagnosesTwoIdsThatAliasSameContainedFile()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var target = temporary.WriteText("Assets/real.txt", "safe");
        var linkCreated = temporary.TryCreateFileLink("Assets/alias.txt", target);
        Assert.True(linkCreated || OperatingSystem.IsWindows(), "Symbolic links must be available on Unix test hosts.");
        if (!linkCreated)
        {
            return;
        }

        var database = AssetDatabase.Scan(new AssetResolver(root));

        Assert.Equal(2, database.Entries.Count);
        Assert.Contains(
            database.Diagnostics,
            static diagnostic => diagnostic.Id == AssetDiagnosticIds.DuplicateTargetAlias);
    }
}
