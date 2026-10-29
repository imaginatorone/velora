using Velora.Rendering;
using Velora.Rendering.Sdl;

namespace Velora.Rendering.Sdl.Tests.TestSupport;

internal sealed class FakeSdlNativeApi : ISdlNativeApi
{
    private readonly Dictionary<nint, PixelSize> _textureSizes = [];
    private ColorRgba _drawColor;
    private nint _nextHandle = 1;

    public string? FailingMethod { get; set; }

    public string? ThrowingMethod { get; set; }

    public Exception ThrownException { get; set; } = new InvalidOperationException("fake native exception");

    public bool IsMainThreadResult { get; set; } = true;

    public string Error { get; set; } = "fake native failure";

    public PixelSize OutputSize { get; set; } = new(1280, 720);

    public Queue<PixelSize> LoadedTextureSizes { get; } = new();

    public List<string> Operations { get; } = [];

    public List<DrawCall> DrawCalls { get; } = [];

    public List<(nint Texture, byte Alpha)> AlphaChanges { get; } = [];

    public List<FillCall> FillCalls { get; } = [];

    public List<LineCall> LineCalls { get; } = [];

    public List<bool> DrawBlendChanges { get; } = [];

    public List<nint> DestroyedTextures { get; } = [];

    public List<nint> DestroyedRenderers { get; } = [];

    public List<nint> DestroyedWindows { get; } = [];

    public int InitializeCount { get; private set; }

    public int ShutdownCount { get; private set; }

    public int CreateWindowCount { get; private set; }

    public (int Width, int Height)? CreatedWindowSize { get; private set; }

    public List<(int Width, int Height)> WindowSizes { get; } = [];

    public int ReadbackCount { get; private set; }

    public FrameReadbackDescriptor ReturnedReadbackDescriptor { get; set; } =
        FrameReadbackDescriptor.CreateBgra8TopDown(new PixelSize(1280, 720));

    public bool IsMainThread()
    {
        Operations.Add("IsMainThread");
        ThrowIf(nameof(IsMainThread));
        return IsMainThreadResult && Succeeds(nameof(IsMainThread));
    }

    public bool InitializeVideo()
    {
        InitializeCount++;
        Operations.Add("InitializeVideo");
        ThrowIf(nameof(InitializeVideo));
        return Succeeds(nameof(InitializeVideo));
    }

    public void ShutdownVideo()
    {
        ShutdownCount++;
        Operations.Add("ShutdownVideo");
        ThrowIf(nameof(ShutdownVideo));
    }

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
        CreateWindowCount++;
        CreatedWindowSize = (width, height);
        window = _nextHandle++;
        renderer = _nextHandle++;
        Operations.Add($"CreateWindow:{title}:{width}x{height}:{resizable}:{highPixelDensity}:{hidden}");
        ThrowIf(nameof(CreateWindowAndRenderer));
        return Succeeds(nameof(CreateWindowAndRenderer));
    }

    public bool SetWindowSize(nint window, int width, int height)
    {
        WindowSizes.Add((width, height));
        Operations.Add($"SetWindowSize:{window}:{width}x{height}");
        ThrowIf(nameof(SetWindowSize));
        return Succeeds(nameof(SetWindowSize));
    }

    public nint CreateLogicalTarget(nint renderer, PixelSize size)
    {
        Operations.Add($"CreateLogicalTarget:{renderer}:{size.Width}x{size.Height}");
        ThrowIf(nameof(CreateLogicalTarget));
        if (!Succeeds(nameof(CreateLogicalTarget)))
        {
            return 0;
        }

        nint handle = _nextHandle++;
        _textureSizes.Add(handle, size);
        return handle;
    }

    public nint LoadTexture(nint renderer, Stream stream, int maximumBytes)
    {
        Operations.Add($"LoadTexture:{renderer}:{maximumBytes}");
        ThrowIf(nameof(LoadTexture));
        if (!Succeeds(nameof(LoadTexture)))
        {
            return 0;
        }

        _ = stream.ReadByte();
        nint handle = _nextHandle++;
        _textureSizes.Add(
            handle,
            LoadedTextureSizes.TryDequeue(out PixelSize size) ? size : new PixelSize(100, 100));
        return handle;
    }

    public bool GetTextureSize(nint texture, out PixelSize size)
    {
        Operations.Add($"GetTextureSize:{texture}");
        ThrowIf(nameof(GetTextureSize));
        return _textureSizes.TryGetValue(texture, out size) && Succeeds(nameof(GetTextureSize));
    }

    public bool ConfigureLoadedTexture(nint texture)
    {
        Operations.Add($"ConfigureLoadedTexture:{texture}");
        ThrowIf(nameof(ConfigureLoadedTexture));
        return Succeeds(nameof(ConfigureLoadedTexture));
    }

    public bool SetTextureAlpha(nint texture, byte alpha)
    {
        AlphaChanges.Add((texture, alpha));
        Operations.Add($"SetTextureAlpha:{texture}:{alpha}");
        ThrowIf(nameof(SetTextureAlpha));
        return Succeeds(nameof(SetTextureAlpha));
    }

    public bool SetRenderTarget(nint renderer, nint texture)
    {
        Operations.Add($"SetRenderTarget:{renderer}:{texture}");
        ThrowIf(nameof(SetRenderTarget));
        return Succeeds(nameof(SetRenderTarget));
    }

    public bool SetDrawColor(nint renderer, ColorRgba color)
    {
        _drawColor = color;
        Operations.Add($"SetDrawColor:{renderer}:{color.R},{color.G},{color.B},{color.A}");
        ThrowIf(nameof(SetDrawColor));
        return Succeeds(nameof(SetDrawColor));
    }

    public bool SetDrawBlendEnabled(nint renderer, bool enabled)
    {
        DrawBlendChanges.Add(enabled);
        Operations.Add($"SetDrawBlendEnabled:{renderer}:{enabled}");
        ThrowIf(nameof(SetDrawBlendEnabled));
        return Succeeds(nameof(SetDrawBlendEnabled));
    }

    public bool Clear(nint renderer)
    {
        Operations.Add($"Clear:{renderer}");
        ThrowIf(nameof(Clear));
        return Succeeds(nameof(Clear));
    }

    public bool FillRect(nint renderer, NativeRectF rectangle)
    {
        FillCalls.Add(new FillCall(rectangle, _drawColor));
        Operations.Add($"FillRect:{renderer}");
        ThrowIf(nameof(FillRect));
        return Succeeds(nameof(FillRect));
    }

    public bool DrawLine(nint renderer, float x1, float y1, float x2, float y2)
    {
        LineCalls.Add(new LineCall(x1, y1, x2, y2, _drawColor));
        Operations.Add($"DrawLine:{renderer}");
        ThrowIf(nameof(DrawLine));
        return Succeeds(nameof(DrawLine));
    }

    public bool DrawTexture(
        nint renderer,
        nint texture,
        NativeRectF destination,
        double rotationDegrees,
        bool flipped)
    {
        DrawCalls.Add(new DrawCall(texture, destination, rotationDegrees, flipped));
        Operations.Add($"DrawTexture:{renderer}:{texture}");
        ThrowIf(nameof(DrawTexture));
        return Succeeds(nameof(DrawTexture));
    }

    public bool GetOutputSize(nint renderer, out PixelSize size)
    {
        size = OutputSize;
        Operations.Add($"GetOutputSize:{renderer}");
        ThrowIf(nameof(GetOutputSize));
        return Succeeds(nameof(GetOutputSize));
    }

    public NativeReadbackResult ReadPixelsBgra(nint renderer, Span<byte> destination)
    {
        ReadbackCount++;
        Operations.Add($"ReadPixelsBgra:{renderer}");
        ThrowIf(nameof(ReadPixelsBgra));
        if (!Succeeds(nameof(ReadPixelsBgra)))
        {
            return NativeReadbackResult.Failure(Error);
        }

        int bytes = checked(1280 * 720 * 4);
        destination[..bytes].Fill(0xA5);
        return NativeReadbackResult.Success(ReturnedReadbackDescriptor, bytes);
    }

    public bool Present(nint renderer)
    {
        Operations.Add($"Present:{renderer}");
        ThrowIf(nameof(Present));
        return Succeeds(nameof(Present));
    }

    public void DestroyTexture(nint texture)
    {
        DestroyedTextures.Add(texture);
        Operations.Add($"DestroyTexture:{texture}");
        ThrowIf(nameof(DestroyTexture));
    }

    public void DestroyRenderer(nint renderer)
    {
        DestroyedRenderers.Add(renderer);
        Operations.Add($"DestroyRenderer:{renderer}");
        ThrowIf(nameof(DestroyRenderer));
    }

    public void DestroyWindow(nint window)
    {
        DestroyedWindows.Add(window);
        Operations.Add($"DestroyWindow:{window}");
        ThrowIf(nameof(DestroyWindow));
    }

    public string GetError() => Error;

    private bool Succeeds(string method) => !string.Equals(FailingMethod, method, StringComparison.Ordinal);

    private void ThrowIf(string method)
    {
        if (string.Equals(ThrowingMethod, method, StringComparison.Ordinal))
        {
            throw ThrownException;
        }
    }
}

internal sealed record DrawCall(
    nint Texture,
    NativeRectF Destination,
    double RotationDegrees,
    bool Flipped);

internal sealed record FillCall(NativeRectF Rectangle, ColorRgba Color);

internal sealed record LineCall(float X1, float Y1, float X2, float Y2, ColorRgba Color);
