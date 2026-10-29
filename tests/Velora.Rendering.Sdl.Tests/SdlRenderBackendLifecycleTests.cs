using Velora.Rendering;
using Velora.Rendering.Sdl.Tests.TestSupport;

namespace Velora.Rendering.Sdl.Tests;

public sealed class SdlRenderBackendLifecycleTests
{
    [Fact]
    public void InitializationRequiresSdlMainThreadBeforeVideoOrWindowAcquisition()
    {
        FakeSdlNativeApi api = new() { IsMainThreadResult = false };
        using SdlRenderBackend backend = CreateBackend(api);

        Assert.False(backend.Initialize(Surface()));

        Assert.Equal("SDL_IsMainThread", backend.LastInitializationError?.Operation);
        Assert.Equal(["IsMainThread"], api.Operations);
        Assert.Equal(0, api.InitializeCount);
        Assert.Equal(0, api.CreateWindowCount);

        api.IsMainThreadResult = true;
        Assert.True(backend.Initialize(Surface()));
    }

    [Fact]
    public void InitializeIsIdempotentAndDisposeReleasesNativeOwnersOnceInOrder()
    {
        FakeSdlNativeApi api = new();
        SdlRenderBackend backend = CreateBackend(api);
        SurfaceDescriptor surface = Surface();

        Assert.True(backend.Initialize(surface));
        Assert.True(backend.Initialize(surface));
        Assert.Equal(1, api.InitializeCount);
        Assert.Equal(1, api.CreateWindowCount);
        Assert.True(backend.IsInitialized);

        backend.Dispose();
        backend.Dispose();

        Assert.False(backend.IsInitialized);
        Assert.Equal([3], api.DestroyedTextures);
        Assert.Equal([2], api.DestroyedRenderers);
        Assert.Equal([1], api.DestroyedWindows);
        Assert.Equal(1, api.ShutdownCount);
        Assert.Equal(
            ["DestroyTexture:3", "DestroyRenderer:2", "DestroyWindow:1", "ShutdownVideo"],
            api.Operations[^4..]);
    }

    [Fact]
    public void FailedInitializationCleansPartialHandlesAndMayBeRetried()
    {
        FakeSdlNativeApi api = new() { FailingMethod = nameof(ISdlNativeApi.CreateWindowAndRenderer) };
        using SdlRenderBackend backend = CreateBackend(api);

        Assert.False(backend.Initialize(Surface()));
        Assert.False(backend.IsInitialized);
        Assert.Equal("SDL_CreateWindowAndRenderer", backend.LastInitializationError?.Operation);
        Assert.Equal([2], api.DestroyedRenderers);
        Assert.Equal([1], api.DestroyedWindows);
        Assert.Equal(1, api.ShutdownCount);

        api.FailingMethod = null;
        Assert.True(backend.Initialize(Surface()));
        Assert.Equal(2, api.InitializeCount);
        Assert.Equal(2, api.CreateWindowCount);
    }

    [Fact]
    public void WindowSizeOverflowOccursBeforeNativeOwnershipAndMayBeRetried()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);
        SurfaceDescriptor overflowing = new(new PixelSize(1280, 720), double.Epsilon);

        Assert.Throws<OverflowException>(() => backend.Initialize(overflowing));

        Assert.Empty(api.Operations);
        Assert.True(backend.Initialize(Surface()));
    }

    [Fact]
    public void ExceptionalInitializationExitReleasesAllAcquiredOwnersAndMayBeRetried()
    {
        FakeSdlNativeApi api = new()
        {
            ThrowingMethod = nameof(ISdlNativeApi.CreateLogicalTarget),
        };
        using SdlRenderBackend backend = CreateBackend(api);

        Assert.Throws<InvalidOperationException>(() => backend.Initialize(Surface()));

        Assert.False(backend.IsInitialized);
        Assert.Equal([2], api.DestroyedRenderers);
        Assert.Equal([1], api.DestroyedWindows);
        Assert.Equal(1, api.ShutdownCount);

        api.ThrowingMethod = null;
        Assert.True(backend.Initialize(Surface()));
    }

    [Fact]
    public void CleanupContinuesAfterOneNativeDestroyThrows()
    {
        FakeSdlNativeApi api = new();
        SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        api.ThrowingMethod = nameof(ISdlNativeApi.DestroyTexture);

        Assert.Throws<InvalidOperationException>(backend.Dispose);

        Assert.False(backend.IsInitialized);
        Assert.Equal([3], api.DestroyedTextures);
        Assert.Equal([2], api.DestroyedRenderers);
        Assert.Equal([1], api.DestroyedWindows);
        Assert.Equal(1, api.ShutdownCount);
    }

    [Fact]
    public void DrawableScaleMapsToWindowPointSizeOnCreateAndResize()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);

        Assert.True(backend.Initialize(new SurfaceDescriptor(new PixelSize(1920, 1080), scaleFactor: 2)));
        backend.Resize(new SurfaceDescriptor(new PixelSize(2560, 1440), scaleFactor: 2));

        Assert.Equal((960, 540), api.CreatedWindowSize);
        Assert.Equal([(1280, 720)], api.WindowSizes);
    }

    [Fact]
    public void CallingNativeLifecycleFromAnotherThreadIsRejected()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Task.Factory.StartNew(
                    () => backend.Resize(Surface()),
                    TestContext.Current.CancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .GetAwaiter()
                .GetResult());

        Assert.Contains("thread-affine", exception.Message, StringComparison.Ordinal);
        Assert.Empty(api.WindowSizes);
    }

    [Fact]
    public void DisposeFromAnotherThreadDoesNotReleaseThreadAffineHandles()
    {
        FakeSdlNativeApi api = new();
        SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Task.Factory.StartNew(
                    backend.Dispose,
                    TestContext.Current.CancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .GetAwaiter()
                .GetResult());

        Assert.Contains("thread-affine", exception.Message, StringComparison.Ordinal);
        Assert.True(backend.IsInitialized);
        Assert.Empty(api.DestroyedTextures);
        backend.Dispose();
        Assert.Equal([3], api.DestroyedTextures);
    }

    [Fact]
    public void InitializationAfterDisposeIsRejected()
    {
        FakeSdlNativeApi api = new();
        SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        backend.Dispose();

        Assert.Throws<ObjectDisposedException>(() => backend.Initialize(Surface()));
    }

    private static SdlRenderBackend CreateBackend(FakeSdlNativeApi api) =>
        new(new FakeTextureAssetSource(), api);

    private static SurfaceDescriptor Surface() => new(new PixelSize(1280, 720));
}
