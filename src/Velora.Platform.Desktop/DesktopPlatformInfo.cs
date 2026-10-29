using System.Globalization;

namespace Velora.Platform.Desktop;

/// <summary>
/// Supplies the desktop host values used by the compatibility built-ins.
/// Tests and deterministic hosts can inject a fixed <see cref="TimeProvider"/>
/// and explicit identity values.
/// </summary>
public sealed class DesktopPlatformInfo : IPlatformInfo
{
    private readonly TimeProvider _timeProvider;

    public DesktopPlatformInfo()
        : this(
            Environment.UserName,
            GetCurrentLocale(),
            TimeProvider.System)
    {
    }

    public DesktopPlatformInfo(
        string userName,
        string preferredLocale,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLocale);
        ArgumentNullException.ThrowIfNull(timeProvider);

        UserName = userName;
        PreferredLocale = preferredLocale;
        _timeProvider = timeProvider;
    }

    public string UserName { get; }

    public string PreferredLocale { get; }

    public string LocalTimeText =>
        _timeProvider.GetLocalNow().ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string GetCurrentLocale()
    {
        string locale = CultureInfo.CurrentUICulture.Name;
        return locale.Length == 0 ? "en" : locale;
    }
}
