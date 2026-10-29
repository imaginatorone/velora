using Velora.Rendering;
using Velora.Rendering.Sdl.Tests.TestSupport;

namespace Velora.Rendering.Sdl.Tests;

public sealed class SdlRenderBackendReadbackAndErrorsTests
{
    [Fact]
    public void ReadbackIsUnavailableBeforeTheFirstSuccessfulRender()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        byte[] destination = new byte[1280 * 720 * 4];

        Assert.False(backend.TryReadFrame(destination, out FrameReadbackResult readback));

        Assert.Equal(
            FrameReadbackDescriptor.CreateBgra8TopDown(new PixelSize(1280, 720)),
            readback.Descriptor);
        Assert.Equal(0, readback.BytesWritten);
        Assert.Equal(0, api.ReadbackCount);
    }

    [Fact]
    public void ReadbackRequiresExactBgraCapacityAfterSuccessfulRenderBeforeCallingNativeApi()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        RenderEmptyFrame(backend);
        byte[] tooSmall = new byte[(1280 * 720 * 4) - 1];

        Assert.False(backend.TryReadFrame(tooSmall, out PixelSize size, out int bytesWritten));

        Assert.Equal(new PixelSize(1280, 720), size);
        Assert.Equal(0, bytesWritten);
        Assert.Equal(0, api.ReadbackCount);
    }

    [Fact]
    public void ReadbackReturnsLogicalTargetAsTightlyPackedBgra()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        RenderEmptyFrame(backend);
        byte[] destination = new byte[1280 * 720 * 4];

        Assert.True(backend.TryReadFrame(destination, out FrameReadbackResult readback));

        Assert.Equal(new PixelSize(1280, 720), readback.Descriptor.Size);
        Assert.Equal(FramePixelFormat.Bgra8, readback.Descriptor.PixelFormat);
        Assert.Equal(FrameRowOrder.TopDown, readback.Descriptor.RowOrder);
        Assert.Equal(1280 * 4, readback.Descriptor.RowPitch);
        Assert.True(readback.Descriptor.IsTightlyPacked);
        Assert.Equal(destination.Length, readback.BytesWritten);
        Assert.All(destination, value => Assert.Equal(0xA5, value));
        Assert.Equal(1, api.ReadbackCount);
        Assert.Contains("SetRenderTarget:2:3", api.Operations);
        Assert.Contains("SetRenderTarget:2:0", api.Operations);
    }

    [Fact]
    public void RenderFailureIsTranslatedWithTheExactOperationAndNativeMessage()
    {
        FakeSdlNativeApi api = new() { FailingMethod = nameof(ISdlNativeApi.Clear) };
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));

        SdlRenderException exception = Assert.Throws<SdlRenderException>(
            () => backend.Render(new RenderFrame(
                new SceneRenderState(null, []),
                LogicalSize.VeloraDefault,
                TimeSpan.Zero)));

        Assert.Equal("SDL_RenderClear(logical target)", exception.Operation);
        Assert.Equal("fake native failure", exception.NativeError);
        Assert.Contains("SDL_RenderClear(logical target)", exception.Message, StringComparison.Ordinal);
        byte[] destination = new byte[1280 * 720 * 4];
        Assert.False(backend.TryReadFrame(destination, out FrameReadbackResult _));
        Assert.Equal(0, api.ReadbackCount);
    }

    [Fact]
    public void ReadbackFailureRestoresWindowTargetBeforeThrowing()
    {
        FakeSdlNativeApi api = new() { FailingMethod = nameof(ISdlNativeApi.ReadPixelsBgra) };
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        RenderEmptyFrame(backend);
        byte[] destination = new byte[1280 * 720 * 4];

        SdlRenderException exception = Assert.Throws<SdlRenderException>(
            () => backend.TryReadFrame(destination, out _, out _));

        Assert.Equal("SDL_RenderReadPixels", exception.Operation);
        Assert.Equal("SetRenderTarget:2:0", api.Operations[^1]);
    }

    [Fact]
    public void RejectsNativeReadbackThatDoesNotMatchTheDeclaredTightLayout()
    {
        FakeSdlNativeApi api = new()
        {
            ReturnedReadbackDescriptor = new FrameReadbackDescriptor(
                new PixelSize(1280, 720),
                FramePixelFormat.Bgra8,
                FrameRowOrder.TopDown,
                (1280 * 4) + 4),
        };
        using SdlRenderBackend backend = CreateBackend(api);
        Assert.True(backend.Initialize(Surface()));
        RenderEmptyFrame(backend);
        byte[] destination = new byte[1280 * 720 * 4];

        SdlRenderException exception = Assert.Throws<SdlRenderException>(
            () => backend.TryReadFrame(destination, out FrameReadbackResult _));

        Assert.Equal("SDL_RenderReadPixels", exception.Operation);
        Assert.Contains("tightly packed", exception.NativeError, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclaresOnlyTheImplementedFallbackCapabilities()
    {
        using SdlRenderBackend backend = CreateBackend(new FakeSdlNativeApi());

        Assert.Equal(
            GraphicsCapabilities.TextureRendering
            | GraphicsCapabilities.RenderTargets
            | GraphicsCapabilities.Readback,
            backend.Capabilities);
        Assert.False(backend.Capabilities.HasFlag(GraphicsCapabilities.Shaders));
        Assert.False(backend.Capabilities.HasFlag(GraphicsCapabilities.PostProcessing));
    }

    private static SdlRenderBackend CreateBackend(FakeSdlNativeApi api) =>
        new(new FakeTextureAssetSource(), api);

    private static void RenderEmptyFrame(SdlRenderBackend backend) =>
        backend.Render(new RenderFrame(
            new SceneRenderState(null, []),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero));

    private static SurfaceDescriptor Surface() => new(new PixelSize(1280, 720));
}
