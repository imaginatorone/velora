using Velora.Platform.Desktop;

namespace Velora.Platform.Desktop.Tests;

public sealed class DesktopApplicationPathsTests
{
    private const string ApplicationId = "com.velora.kitchen-sink";

    [Fact]
    public void ResolvesWindowsLocalApplicationDataRoot()
    {
        string localData = Absolute("windows-local-data");
        var environment = Environment(DesktopPlatformKind.Windows) with
        {
            LocalApplicationData = localData,
        };

        DesktopApplicationPaths paths = DesktopApplicationPaths.Resolve(ApplicationId, environment);

        Assert.Equal(Path.Combine(localData, ApplicationId), paths.UserDataRoot);
        Assert.Equal(Path.Combine(localData, ApplicationId, "Saves"), paths.SaveRoot);
        Assert.Equal(Path.Combine(localData, ApplicationId, "ScriptData"), paths.ScriptDataRoot);
    }

    [Fact]
    public void ResolvesMacOsApplicationSupportRoot()
    {
        string home = Absolute("mac-home");
        DesktopHostEnvironment environment = Environment(DesktopPlatformKind.MacOS) with
        {
            HomeDirectory = home,
        };

        DesktopApplicationPaths paths = DesktopApplicationPaths.Resolve(ApplicationId, environment);

        Assert.Equal(
            Path.Combine(home, "Library", "Application Support", ApplicationId),
            paths.UserDataRoot);
    }

    [Fact]
    public void LinuxUsesAbsoluteXdgDataHome()
    {
        string xdg = Absolute("xdg-data");
        DesktopHostEnvironment environment = Environment(DesktopPlatformKind.Linux) with
        {
            XdgDataHome = xdg,
        };

        DesktopApplicationPaths paths = DesktopApplicationPaths.Resolve(ApplicationId, environment);

        Assert.Equal(Path.Combine(xdg, ApplicationId), paths.UserDataRoot);
    }

    [Fact]
    public void LinuxIgnoresRelativeXdgDataHomeAndUsesHomeFallback()
    {
        string home = Absolute("linux-home");
        DesktopHostEnvironment environment = Environment(DesktopPlatformKind.Linux) with
        {
            HomeDirectory = home,
            XdgDataHome = "relative/data",
        };

        DesktopApplicationPaths paths = DesktopApplicationPaths.Resolve(ApplicationId, environment);

        Assert.Equal(Path.Combine(home, ".local", "share", ApplicationId), paths.UserDataRoot);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("absolute/path")]
    [InlineData("contains space")]
    [InlineData(".hidden")]
    [InlineData("trailing-")]
    public void RejectsNonPortableApplicationIdentifiers(string applicationId)
    {
        Assert.Throws<ArgumentException>(() =>
            DesktopApplicationPaths.Resolve(applicationId, Environment(DesktopPlatformKind.Linux)));
    }

    [Fact]
    public void RejectsMissingRequiredPlatformRootsAndUnknownPlatform()
    {
        DesktopHostEnvironment missingWindowsRoot = Environment(DesktopPlatformKind.Windows) with
        {
            LocalApplicationData = null,
        };
        Assert.Throws<InvalidOperationException>(() =>
            DesktopApplicationPaths.Resolve(ApplicationId, missingWindowsRoot));

        DesktopHostEnvironment unknown = Environment((DesktopPlatformKind)999);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DesktopApplicationPaths.Resolve(ApplicationId, unknown));
    }

    private static DesktopHostEnvironment Environment(DesktopPlatformKind platform) => new()
    {
        Platform = platform,
        HomeDirectory = Absolute("home"),
        LocalApplicationData = Absolute("local-data"),
    };

    private static string Absolute(string leaf) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
}
