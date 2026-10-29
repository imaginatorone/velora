namespace Velora.Platform;

public enum ApplicationLifecycleEvent
{
    Suspending,
    Resumed,
    LowMemory,
}

/// <summary>
/// Receives lifecycle events originating from a platform host. Player.Core is
/// responsible for translating them into capture/restore policy.
/// </summary>
public interface IApplicationLifecycleSink
{
    void Handle(ApplicationLifecycleEvent lifecycleEvent);
}
