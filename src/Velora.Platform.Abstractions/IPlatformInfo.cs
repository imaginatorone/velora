namespace Velora.Platform;

/// <summary>
/// Supplies the ambient values used by compatibility built-ins. Runtime tests
/// and replay provide deterministic implementations.
/// </summary>
public interface IPlatformInfo
{
    string UserName { get; }

    string PreferredLocale { get; }

    /// <summary>Returns local time text in the legacy HH:mm representation.</summary>
    string LocalTimeText { get; }
}
