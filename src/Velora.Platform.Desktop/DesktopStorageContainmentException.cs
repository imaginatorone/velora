namespace Velora.Platform.Desktop;

/// <summary>
/// Raised when a logical storage name reaches a symbolic link or reparse point
/// whose final target is outside the configured application root.
/// </summary>
public sealed class DesktopStorageContainmentException : IOException
{
    internal DesktopStorageContainmentException()
        : base("The storage path resolves outside its allowed application-data root.")
    {
    }
}
