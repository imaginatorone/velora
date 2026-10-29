using Velora.Platform.Desktop;

namespace Velora.Platform.Desktop.Tests;

public sealed class DesktopLifecycleAdapterTests
{
    [Fact]
    public void DispatchesHostLifecycleCallbacksInOrder()
    {
        var sink = new RecordingSink();
        var adapter = new DesktopLifecycleAdapter(sink);

        adapter.Suspending();
        adapter.Resumed();
        adapter.LowMemory();

        Assert.Equal(
            [
                ApplicationLifecycleEvent.Suspending,
                ApplicationLifecycleEvent.Resumed,
                ApplicationLifecycleEvent.LowMemory,
            ],
            sink.Events);
    }

    [Fact]
    public void RejectsUnknownLifecycleEvent()
    {
        var adapter = new DesktopLifecycleAdapter(new RecordingSink());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            adapter.Dispatch((ApplicationLifecycleEvent)999));
    }

    private sealed class RecordingSink : IApplicationLifecycleSink
    {
        internal List<ApplicationLifecycleEvent> Events { get; } = [];

        public void Handle(ApplicationLifecycleEvent lifecycleEvent)
        {
            Events.Add(lifecycleEvent);
        }
    }
}
