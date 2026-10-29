namespace Velora.Platform.Desktop;

/// <summary>
/// Creates isolated save and script-data services beneath the platform's
/// application-specific per-user root.
/// </summary>
public sealed record DesktopStorageServices
{
    private DesktopStorageServices(ISaveStorage saves, IScriptDataStore scriptData)
    {
        Saves = saves;
        ScriptData = scriptData;
    }

    public ISaveStorage Saves { get; }

    public IScriptDataStore ScriptData { get; }

    public static DesktopStorageServices Create(
        string applicationId,
        DesktopHostEnvironment? environment = null,
        int maximumFileBytes = DesktopStorageLimits.MaximumFileBytes)
    {
        DesktopApplicationPaths paths = DesktopApplicationPaths.Resolve(applicationId, environment);
        return new DesktopStorageServices(
            new DesktopSaveStorage(paths.SaveRoot, maximumFileBytes),
            new DesktopScriptDataStore(paths.ScriptDataRoot, maximumFileBytes));
    }
}
