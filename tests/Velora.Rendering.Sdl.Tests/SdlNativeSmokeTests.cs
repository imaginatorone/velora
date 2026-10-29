using System.Runtime.InteropServices;
using Velora.Assets;
using Velora.Rendering;

namespace Velora.Rendering.Sdl.Tests;

public sealed class SdlNativeSmokeTests
{
    [Fact]
    public void HiddenWindowRenderAndReadbackWhenExplicitlyEnabledAndNativeLibraryIsAvailable()
    {
        if (!NativeLibrary.TryLoad("SDL3", out nint library))
        {
            Assert.Skip("The SDL3 native library is not available to this test process.");
        }

        NativeLibrary.Free(library);
        if (!NativeLibrary.TryLoad("SDL3_image", out nint imageLibrary))
        {
            Assert.Skip("The SDL3_image native library is not available to this test process.");
        }

        NativeLibrary.Free(imageLibrary);
        if (!string.Equals(
                Environment.GetEnvironmentVariable("VELORA_RUN_SDL_NATIVE_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Skip("Set VELORA_RUN_SDL_NATIVE_SMOKE=1 to opt in to native SDL window creation.");
        }

        if (!SDL3.SDL.IsMainThread())
        {
            Assert.Skip("The test runner is not executing this test on SDL's OS main thread.");
        }

        using SdlRenderBackend backend = new(
            new BmpTextureSource(),
            new SdlRenderBackendOptions(createHiddenWindow: true));
        bool initialized = backend.Initialize(new SurfaceDescriptor(new PixelSize(1280, 720)));
        Assert.True(
            initialized,
            backend.LastInitializationError is null
                ? "SDL initialization failed without a translated error."
                : $"{backend.LastInitializationError.Operation}: {backend.LastInitializationError.Message}");

        backend.Render(new RenderFrame(
            new SceneRenderState("smoke.bmp", []),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero));
        byte[] frame = new byte[1280 * 720 * 4];
        Assert.True(backend.TryReadFrame(frame, out FrameReadbackResult readback));
        Assert.Equal(FramePixelFormat.Bgra8, readback.Descriptor.PixelFormat);
        Assert.Equal(FrameRowOrder.TopDown, readback.Descriptor.RowOrder);
        Assert.True(readback.Descriptor.IsTightlyPacked);
        Assert.Equal(frame.Length, readback.BytesWritten);
        Assert.Empty(backend.TextureFailures);

        AssertPixel(frame, readback.Descriptor, 0, 0, [0x00, 0x00, 0xFF, 0xFF]);
        AssertPixel(frame, readback.Descriptor, 1279, 0, [0x00, 0xFF, 0x00, 0xFF]);
        AssertPixel(frame, readback.Descriptor, 0, 719, [0xFF, 0x00, 0x00, 0xFF]);
        AssertPixel(frame, readback.Descriptor, 1279, 719, [0xFF, 0xFF, 0xFF, 0xFF]);
    }

    private static void AssertPixel(
        byte[] frame,
        FrameReadbackDescriptor descriptor,
        int x,
        int y,
        byte[] expected)
    {
        int offset = checked((y * descriptor.RowPitch) + (x * FrameReadbackDescriptor.BytesPerPixel));
        Assert.Equal(expected, frame[offset..(offset + FrameReadbackDescriptor.BytesPerPixel)]);
    }

    private sealed class BmpTextureSource : ITextureAssetSource
    {
        private static readonly byte[] TwoByTwoBmp =
        [
            0x42, 0x4D, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00,
            0x00, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
            0xFF, 0x00, 0xFF, 0x00, 0x00, 0x00,
        ];

        public TextureAssetReadResult OpenRead(AssetId assetId) =>
            assetId.Value == "smoke.bmp"
                ? TextureAssetReadResult.Success(new MemoryStream(TwoByTwoBmp, writable: false))
                : TextureAssetReadResult.Failure($"Unexpected smoke-test asset '{assetId}'.");
    }
}
