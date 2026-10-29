using System.Text;
using Velora.Assets.Diagnostics;
using Velora.Assets.Tests.TestSupport;

namespace Velora.Assets.Tests.Assets;

public sealed class AssetResolverTests
{
    [Fact]
    public void ResolveAndOpenReadUseCanonicalPathAndPortableSeparators()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("project/Assets");
        temporary.WriteText("project/Assets/images/hero.txt", "hero");
        var resolver = new AssetResolver(root);

        var resolution = resolver.Resolve(@"images\hero.txt");
        var opened = resolver.OpenRead(@"images\hero.txt");

        Assert.True(resolution.IsSuccess);
        Assert.Equal(
            Path.Combine(resolver.RootDirectory, "images", "hero.txt"),
            resolution.FullPath);
        Assert.Equal(AssetOrigin.PrimaryRoot, resolution.Origin);
        Assert.True(opened.IsSuccess);
        using var stream = Assert.IsType<FileStream>(opened.Stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("hero", reader.ReadToEnd());
    }

    [Fact]
    public void ResolvePreservesBenignIntraRootNormalization()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("project/Assets");
        temporary.WriteText("project/Assets/ui/icon.txt", "icon");
        var resolver = new AssetResolver(root);

        var resolution = resolver.Resolve("sprites/../ui/icon.txt");

        Assert.True(resolution.IsSuccess);
        Assert.Equal("ui/icon.txt", resolution.AssetId?.Value);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("safe/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData(@"C:\Windows\system.ini")]
    [InlineData(@"\\server\share\secret")]
    public void ResolveRejectsTraversalAndRootedInputsBeforeFilesystemAccess(string candidate)
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var resolver = new AssetResolver(root);

        var resolution = resolver.Resolve(candidate);

        Assert.False(resolution.IsSuccess);
        Assert.Equal(AssetDiagnosticIds.InvalidLogicalPath, resolution.Diagnostic?.Id);
    }

    [Fact]
    public void ResolveDiagnosesCaseMismatchOnEveryHostFilesystem()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        temporary.WriteText("Assets/Images/Hero.PNG", "hero");
        var resolver = new AssetResolver(root);

        var exact = resolver.Resolve("Images/Hero.PNG");
        var mismatched = resolver.Resolve("images/hero.png");

        Assert.True(exact.IsSuccess);
        Assert.False(mismatched.IsSuccess);
        Assert.Equal(AssetDiagnosticIds.CaseMismatch, mismatched.Diagnostic?.Id);
        Assert.Equal("Images/Hero.PNG", mismatched.Diagnostic?.SuggestedLogicalPath);
    }

    [Fact]
    public void ResolveRejectsExistingAndMissingTargetsThroughEscapingDirectoryLink()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("project/Assets");
        var outside = temporary.CreateDirectory("project/Assets-other");
        temporary.WriteText("project/Assets-other/secret.txt", "secret");
        var linkCreated = temporary.TryCreateDirectoryLink("project/Assets/linked", outside);
        Assert.True(linkCreated || OperatingSystem.IsWindows(), "Symbolic links must be available on Unix test hosts.");
        if (!linkCreated)
        {
            return;
        }

        var resolver = new AssetResolver(root);
        var existing = resolver.Resolve("linked/secret.txt");
        var missing = resolver.Resolve("linked/not-created-yet.txt");

        Assert.Equal(AssetDiagnosticIds.ContainmentViolation, existing.Diagnostic?.Id);
        Assert.Equal(AssetDiagnosticIds.ContainmentViolation, missing.Diagnostic?.Id);
    }

    [Fact]
    public void ResolveAllowsInternalLinkOnlyWhenFinalTargetRemainsContained()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var target = temporary.CreateDirectory("Assets/real");
        temporary.WriteText("Assets/real/file.txt", "safe");
        var linkCreated = temporary.TryCreateDirectoryLink("Assets/alias", target);
        Assert.True(linkCreated || OperatingSystem.IsWindows(), "Symbolic links must be available on Unix test hosts.");
        if (!linkCreated)
        {
            return;
        }

        var resolver = new AssetResolver(root);
        var resolution = resolver.Resolve("alias/file.txt");

        Assert.True(resolution.IsSuccess);
        Assert.Equal(
            Path.Combine(resolver.RootDirectory, "real", "file.txt"),
            resolution.FullPath);
    }

    [Fact]
    public void ResolveUsesComponentAwareMountPrefixAndCanonicalMountRoot()
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.CreateDirectory("Assets");
        var mountRoot = temporary.CreateDirectory("DownloadedContent");
        temporary.WriteText("DownloadedContent/chapter.txt", "mounted");
        temporary.WriteText("Assets/dlc-extra/chapter.txt", "primary");
        var resolver = new AssetResolver(
            root,
            new[] { AssetMount.Create("dlc", mountRoot) });

        var mounted = resolver.Resolve("dlc/chapter.txt");
        var similarlyNamed = resolver.Resolve("dlc-extra/chapter.txt");

        Assert.True(mounted.IsSuccess);
        Assert.Equal(AssetOrigin.Mount, mounted.Origin);
        Assert.True(similarlyNamed.IsSuccess);
        Assert.Equal(AssetOrigin.PrimaryRoot, similarlyNamed.Origin);
    }

    [Fact]
    public void ResolveMissingAssetReturnsActionableDiagnostic()
    {
        using var temporary = new TemporaryDirectory();
        var resolver = new AssetResolver(temporary.CreateDirectory("Assets"));

        var resolution = resolver.Resolve("images/missing.png");

        Assert.False(resolution.IsSuccess);
        Assert.Equal(AssetDiagnosticIds.MissingAsset, resolution.Diagnostic?.Id);
        Assert.Equal("images/missing.png", resolution.Diagnostic?.LogicalPath);
    }
}
