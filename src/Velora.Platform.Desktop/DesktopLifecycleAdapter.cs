namespace Velora.Platform.Desktop;

/// <summary>
/// Small host boundary that translates desktop application callbacks into the
/// platform-neutral lifecycle sink.
/// </summary>
public sealed class DesktopLifecycleAdapter
{
    private readonly IApplicationLifecycleSink _sink;

    public DesktopLifecycleAdapter(IApplicationLifecycleSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    public void Suspending() => Dispatch(ApplicationLifecycleEvent.Suspending);

    public void Resumed() => Dispatch(ApplicationLifecycleEvent.Resumed);

    public void LowMemory() => Dispatch(ApplicationLifecycleEvent.LowMemory);

    public void Dispatch(ApplicationLifecycleEvent lifecycleEvent)
    {
        if (!Enum.IsDefined(lifecycleEvent))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleEvent), lifecycleEvent, "Unknown lifecycle event.");
        }

        _sink.Handle(lifecycleEvent);
    }
}
