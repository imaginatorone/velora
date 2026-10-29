namespace Velora.Platform.Desktop;

public enum DesktopPlatformKind
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>
/// Injectable snapshot of the environment inputs used to select the desktop
/// per-user application-data root.
/// </summary>
public sealed record DesktopHostEnvironment
{
    public required DesktopPlatformKind Platform { get; init; }

    public required string HomeDirectory { get; init; }

    public string? LocalApplicationData { get; init; }

    public string? XdgDataHome { get; init; }

    public static DesktopHostEnvironment Capture()
    {
        DesktopPlatformKind platform = DetectPlatform();
        return new DesktopHostEnvironment
        {
            Platform = platform,
            HomeDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolderOption.DoNotVerify),
            LocalApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            XdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        };
    }

    private static DesktopPlatformKind DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return DesktopPlatformKind.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return DesktopPlatformKind.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return DesktopPlatformKind.Linux;
        }

        throw new PlatformNotSupportedException("Velora's desktop host supports Windows, macOS, and Linux.");
    }
}

/// <summary>
/// Host-only filesystem roots for one installed game. These values are never
/// surfaced through <see cref="IScriptDataStore"/>.
/// </summary>
public sealed record DesktopApplicationPaths
{
    private const int MaximumApplicationIdLength = 128;

    private DesktopApplicationPaths(string userDataRoot)
    {
        UserDataRoot = userDataRoot;
        SaveRoot = Path.Combine(userDataRoot, "Saves");
        ScriptDataRoot = Path.Combine(userDataRoot, "ScriptData");
    }

    public string UserDataRoot { get; }

    public string SaveRoot { get; }

    public string ScriptDataRoot { get; }

    public static DesktopApplicationPaths Resolve(
        string applicationId,
        DesktopHostEnvironment? environment = null)
    {
        ValidateApplicationId(applicationId);
        environment ??= DesktopHostEnvironment.Capture();

        if (!Enum.IsDefined(environment.Platform))
        {
            throw new ArgumentOutOfRangeException(
                nameof(environment),
                environment.Platform,
                "Unknown desktop platform kind.");
        }

        string baseDirectory = environment.Platform switch
        {
            DesktopPlatformKind.Windows => RequireAbsolute(
                environment.LocalApplicationData,
                "The Windows local application-data directory is unavailable."),
            DesktopPlatformKind.MacOS => Path.Combine(
                RequireAbsolute(
                    environment.HomeDirectory,
                    "The macOS home directory is unavailable."),
                "Library",
                "Application Support"),
            DesktopPlatformKind.Linux => ResolveLinuxDataRoot(environment),
            _ => throw new System.Diagnostics.UnreachableException(),
        };

        string root = Path.GetFullPath(Path.Combine(baseDirectory, applicationId));
        return new DesktopApplicationPaths(Path.TrimEndingDirectorySeparator(root));
    }

    private static string ResolveLinuxDataRoot(DesktopHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(environment.XdgDataHome)
            && Path.IsPathFullyQualified(environment.XdgDataHome))
        {
            return Path.GetFullPath(environment.XdgDataHome);
        }

        return Path.Combine(
            RequireAbsolute(
                environment.HomeDirectory,
                "The Linux home directory is unavailable."),
            ".local",
            "share");
    }

    private static string RequireAbsolute(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(message);
        }

        return Path.GetFullPath(path);
    }

    private static void ValidateApplicationId(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        if (applicationId.Length > MaximumApplicationIdLength
            || !char.IsAsciiLetterOrDigit(applicationId[0])
            || !char.IsAsciiLetterOrDigit(applicationId[^1])
            || applicationId.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException(
                "The application ID must be a portable 1-128 character identifier using letters, digits, '.', '-', or '_'.",
                nameof(applicationId));
        }
    }
}
