using Velora.Rendering;

namespace Velora.Rendering.Sdl;

internal readonly record struct NativeRectF(float X, float Y, float Width, float Height)
{
    public static NativeRectF From(RectD value) => new(
        checked((float)value.X),
        checked((float)value.Y),
        checked((float)value.Width),
        checked((float)value.Height));
}

internal readonly record struct NativeReadbackResult(
    bool IsSuccess,
    FrameReadbackDescriptor Descriptor,
    int BytesWritten,
    string? Error)
{
    public static NativeReadbackResult Success(FrameReadbackDescriptor descriptor, int bytesWritten) =>
        new(true, descriptor, bytesWritten, null);

    public static NativeReadbackResult Failure(string error) =>
        new(false, default, 0, error);
}

internal interface ISdlNativeApi
{
    bool IsMainThread();

    bool InitializeVideo();

    void ShutdownVideo();

    bool CreateWindowAndRenderer(
        string title,
        int width,
        int height,
        bool resizable,
        bool highPixelDensity,
        bool hidden,
        out nint window,
        out nint renderer);

    bool SetWindowSize(nint window, int width, int height);

    nint CreateLogicalTarget(nint renderer, PixelSize size);

    nint LoadTexture(nint renderer, Stream stream, int maximumBytes);

    bool GetTextureSize(nint texture, out PixelSize size);

    bool ConfigureLoadedTexture(nint texture);

    bool SetTextureAlpha(nint texture, byte alpha);

    bool SetRenderTarget(nint renderer, nint texture);

    bool SetDrawColor(nint renderer, ColorRgba color);

    bool SetDrawBlendEnabled(nint renderer, bool enabled);

    bool Clear(nint renderer);

    bool FillRect(nint renderer, NativeRectF rectangle);

    bool DrawLine(nint renderer, float x1, float y1, float x2, float y2);

    bool DrawTexture(
        nint renderer,
        nint texture,
        NativeRectF destination,
        double rotationDegrees,
        bool flipped);

    bool GetOutputSize(nint renderer, out PixelSize size);

    NativeReadbackResult ReadPixelsBgra(nint renderer, Span<byte> destination);

    bool Present(nint renderer);

    void DestroyTexture(nint texture);

    void DestroyRenderer(nint renderer);

    void DestroyWindow(nint window);

    string GetError();
}

internal sealed class NativeHandleOwner : IDisposable
{
    private readonly Action<nint> _destroy;
    private readonly ThreadAffinity _threadAffinity;
    private nint _handle;

    public NativeHandleOwner(nint handle, ThreadAffinity threadAffinity, Action<nint> destroy)
    {
        if (handle == 0)
        {
            throw new ArgumentException("A non-zero native handle is required.", nameof(handle));
        }

        ArgumentNullException.ThrowIfNull(threadAffinity);
        ArgumentNullException.ThrowIfNull(destroy);
        _handle = handle;
        _threadAffinity = threadAffinity;
        _destroy = destroy;
    }

    public nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle == 0, this);
            return _handle;
        }
    }

    public bool IsDisposed => _handle == 0;

    public void Dispose()
    {
        if (_handle == 0)
        {
            return;
        }

        _threadAffinity.AssertCurrent();
        nint handle = _handle;
        _handle = 0;
        _destroy(handle);
        GC.SuppressFinalize(this);
    }
}

internal sealed record TextureCacheEntry(NativeHandleOwner Owner, PixelSize Size);
