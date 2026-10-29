using System.Runtime.InteropServices;
using SdlApi = SDL3.SDL;
using SdlImage = SDL3.Image;
using Velora.Rendering;

namespace Velora.Rendering.Sdl;

internal sealed class SdlNativeApi : ISdlNativeApi
{
    public bool IsMainThread() => SdlApi.IsMainThread();

    public bool InitializeVideo() => SdlApi.Init(SdlApi.InitFlags.Video);

    public void ShutdownVideo() => SdlApi.QuitSubSystem(SdlApi.InitFlags.Video);

    public bool CreateWindowAndRenderer(
        string title,
        int width,
        int height,
        bool resizable,
        bool highPixelDensity,
        bool hidden,
        out nint window,
        out nint renderer)
    {
        SdlApi.WindowFlags flags = 0;
        if (resizable)
        {
            flags |= SdlApi.WindowFlags.Resizable;
        }

        if (highPixelDensity)
        {
            flags |= SdlApi.WindowFlags.HighPixelDensity;
        }

        if (hidden)
        {
            flags |= SdlApi.WindowFlags.Hidden;
        }

        return SdlApi.CreateWindowAndRenderer(title, width, height, flags, out window, out renderer);
    }

    public bool SetWindowSize(nint window, int width, int height) =>
        SdlApi.SetWindowSize(window, width, height);

    public nint CreateLogicalTarget(nint renderer, PixelSize size)
    {
        nint texture = SdlApi.CreateTexture(
            renderer,
            SdlApi.PixelFormat.RGBA8888,
            SdlApi.TextureAccess.Target,
            size.Width,
            size.Height);
        if (texture == 0)
        {
            return 0;
        }

        if (SdlApi.SetTextureBlendMode(texture, SdlApi.BlendMode.None)
            && SdlApi.SetTextureScaleMode(texture, SdlApi.ScaleMode.Linear))
        {
            return texture;
        }

        SdlApi.DestroyTexture(texture);
        return 0;
    }

    public nint LoadTexture(nint renderer, Stream stream, int maximumBytes)
    {
        using SdlApi.IOStreamOwner io = SdlApi.IOFromStream(
            stream,
            maximumBytes,
            closeIoOnDispose: true);
        return SdlImage.LoadTextureIO(renderer, io.Handle, closeio: false);
    }

    public bool GetTextureSize(nint texture, out PixelSize size)
    {
        size = default;
        if (!SdlApi.GetTextureSize(texture, out float width, out float height)
            || !float.IsFinite(width)
            || !float.IsFinite(height)
            || width <= 0
            || height <= 0
            || width > int.MaxValue
            || height > int.MaxValue)
        {
            return false;
        }

        int integerWidth = checked((int)MathF.Round(width));
        int integerHeight = checked((int)MathF.Round(height));
        if (integerWidth <= 0 || integerHeight <= 0)
        {
            return false;
        }

        size = new PixelSize(integerWidth, integerHeight);
        return true;
    }

    public bool ConfigureLoadedTexture(nint texture) =>
        SdlApi.SetTextureBlendMode(texture, SdlApi.BlendMode.Blend)
        && SdlApi.SetTextureScaleMode(texture, SdlApi.ScaleMode.Linear);

    public bool SetTextureAlpha(nint texture, byte alpha) => SdlApi.SetTextureAlphaMod(texture, alpha);

    public bool SetRenderTarget(nint renderer, nint texture) => SdlApi.SetRenderTarget(renderer, texture);

    public bool SetDrawColor(nint renderer, ColorRgba color) =>
        SdlApi.SetRenderDrawColor(renderer, color.R, color.G, color.B, color.A);

    public bool SetDrawBlendEnabled(nint renderer, bool enabled) =>
        SdlApi.SetRenderDrawBlendMode(
            renderer,
            enabled ? SdlApi.BlendMode.Blend : SdlApi.BlendMode.None);

    public bool Clear(nint renderer) => SdlApi.RenderClear(renderer);

    public bool FillRect(nint renderer, NativeRectF rectangle)
    {
        SdlApi.FRect nativeRectangle = new()
        {
            X = rectangle.X,
            Y = rectangle.Y,
            W = rectangle.Width,
            H = rectangle.Height,
        };
        return SdlApi.RenderFillRect(renderer, in nativeRectangle);
    }

    public bool DrawLine(nint renderer, float x1, float y1, float x2, float y2) =>
        SdlApi.RenderLine(renderer, x1, y1, x2, y2);

    public bool DrawTexture(
        nint renderer,
        nint texture,
        NativeRectF destination,
        double rotationDegrees,
        bool flipped)
    {
        SdlApi.FRect destinationRect = new()
        {
            X = destination.X,
            Y = destination.Y,
            W = destination.Width,
            H = destination.Height,
        };

        if (rotationDegrees == 0 && !flipped)
        {
            return SdlApi.RenderTexture(renderer, texture, nint.Zero, in destinationRect);
        }

        SdlApi.FPoint center = new()
        {
            X = destination.Width * 0.5f,
            Y = destination.Height * 0.5f,
        };
        SdlApi.FlipMode flipMode = flipped ? SdlApi.FlipMode.Horizontal : SdlApi.FlipMode.None;
        return SdlApi.RenderTextureRotated(
            renderer,
            texture,
            nint.Zero,
            in destinationRect,
            rotationDegrees,
            in center,
            flipMode);
    }

    public bool GetOutputSize(nint renderer, out PixelSize size)
    {
        size = default;
        if (!SdlApi.GetRenderOutputSize(renderer, out int width, out int height)
            || width <= 0
            || height <= 0)
        {
            return false;
        }

        size = new PixelSize(width, height);
        return true;
    }

    public unsafe NativeReadbackResult ReadPixelsBgra(nint renderer, Span<byte> destination)
    {
        nint sourceSurface = SdlApi.RenderReadPixels(renderer, null);
        if (sourceSurface == 0)
        {
            return NativeReadbackResult.Failure(GetError());
        }

        nint convertedSurface = 0;
        try
        {
            SdlApi.PixelFormat bgraMemoryFormat = BitConverter.IsLittleEndian
                ? SdlApi.PixelFormat.ARGB8888
                : SdlApi.PixelFormat.BGRA8888;
            convertedSurface = SdlApi.ConvertSurface(sourceSurface, bgraMemoryFormat);
            if (convertedSurface == 0)
            {
                return NativeReadbackResult.Failure(GetError());
            }

            SdlApi.Surface surface = Marshal.PtrToStructure<SdlApi.Surface>(convertedSurface);
            if (surface.Width <= 0
                || surface.Height <= 0
                || surface.Pixels == 0
                || surface.Pitch < checked(surface.Width * 4))
            {
                return NativeReadbackResult.Failure("SDL returned an invalid BGRA readback surface.");
            }

            int rowBytes = checked(surface.Width * 4);
            int requiredBytes = checked(rowBytes * surface.Height);
            if (destination.Length < requiredBytes)
            {
                return NativeReadbackResult.Failure(
                    $"The readback destination requires at least {requiredBytes} bytes.");
            }

            for (int row = 0; row < surface.Height; row++)
            {
                byte* source = (byte*)surface.Pixels + (row * surface.Pitch);
                new ReadOnlySpan<byte>(source, rowBytes)
                    .CopyTo(destination.Slice(row * rowBytes, rowBytes));
            }

            FrameReadbackDescriptor descriptor = FrameReadbackDescriptor.CreateBgra8TopDown(
                new PixelSize(surface.Width, surface.Height));
            return NativeReadbackResult.Success(descriptor, requiredBytes);
        }
        finally
        {
            if (convertedSurface != 0 && convertedSurface != sourceSurface)
            {
                SdlApi.DestroySurface(convertedSurface);
            }

            SdlApi.DestroySurface(sourceSurface);
        }
    }

    public bool Present(nint renderer) => SdlApi.RenderPresent(renderer);

    public void DestroyTexture(nint texture) => SdlApi.DestroyTexture(texture);

    public void DestroyRenderer(nint renderer) => SdlApi.DestroyRenderer(renderer);

    public void DestroyWindow(nint window) => SdlApi.DestroyWindow(window);

    public string GetError() => SdlApi.GetError();
}
