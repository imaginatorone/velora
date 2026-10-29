using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Velora.Assets;
using Velora.Rendering;

namespace Velora.Rendering.Sdl;

/// <summary>
/// SDL3 Render API fallback backend. Every native call and destruction must
/// occur on the thread that successfully initialized the backend.
/// </summary>
public sealed class SdlRenderBackend : IRenderBackend
{
    private static readonly PixelSize LogicalTargetSize = new(1280, 720);
    private static readonly FrameReadbackDescriptor LogicalReadbackDescriptor =
        FrameReadbackDescriptor.CreateBgra8TopDown(LogicalTargetSize);

    private readonly ISdlNativeApi _api;
    private readonly Dictionary<string, LinkedListNode<TextureCacheRecord>> _textureCache =
        new(StringComparer.Ordinal);
    private readonly LinkedList<TextureCacheRecord> _textureLru = new();
    private readonly ITextureAssetSource _textureSource;
    private readonly ThreadAffinity _threadAffinity = new();
    private readonly SdlRenderBackendOptions _options;
    private NativeHandleOwner? _logicalTarget;
    private NativeHandleOwner? _renderer;
    private NativeHandleOwner? _window;
    private bool _disposed;
    private bool _hasRenderedFrame;
    private bool _videoInitialized;

    public SdlRenderBackend(
        ITextureAssetSource textureSource,
        SdlRenderBackendOptions? options = null)
        : this(textureSource, new SdlNativeApi(), options)
    {
    }

    public SdlRenderBackend(
        AssetResolver assetResolver,
        SdlRenderBackendOptions? options = null)
        : this(new AssetResolverTextureSource(assetResolver), new SdlNativeApi(), options)
    {
    }

    internal SdlRenderBackend(
        ITextureAssetSource textureSource,
        ISdlNativeApi api,
        SdlRenderBackendOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(textureSource);
        ArgumentNullException.ThrowIfNull(api);
        _textureSource = textureSource;
        _api = api;
        _options = options ?? new SdlRenderBackendOptions();
    }

    public bool IsInitialized { get; private set; }

    public GraphicsCapabilities Capabilities =>
        GraphicsCapabilities.TextureRendering
        | GraphicsCapabilities.RenderTargets
        | GraphicsCapabilities.Readback;

    public SurfaceDescriptor? Surface { get; private set; }

    public LetterboxLayout? LastLayout { get; private set; }

    public SdlInitializationError? LastInitializationError { get; private set; }

    public IReadOnlyList<TextureLoadFailure> TextureFailures =>
        new ReadOnlyCollection<TextureLoadFailure>(
            _textureLru
                .Where(static record => record.Failure is not null)
                .Select(static record => record.Failure!)
                .ToArray());

    public bool Initialize(SurfaceDescriptor surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsInitialized)
        {
            _threadAffinity.AssertCurrent();
            if (Surface != surface)
            {
                Resize(surface);
            }

            return true;
        }

        PixelSize windowSize = CalculateWindowSize(surface);
        _threadAffinity.BindCurrent();
        LastInitializationError = null;
        try
        {
            if (!_api.IsMainThread())
            {
                return FailInitialization(
                    "SDL_IsMainThread",
                    errorOverride: "SDL window creation must run on SDL's main thread.");
            }

            if (!_api.InitializeVideo())
            {
                return FailInitialization("SDL_Init(SDL_INIT_VIDEO)");
            }

            _videoInitialized = true;
            bool created = _api.CreateWindowAndRenderer(
                _options.WindowTitle,
                windowSize.Width,
                windowSize.Height,
                _options.Resizable,
                _options.HighPixelDensity,
                _options.CreateHiddenWindow,
                out nint window,
                out nint renderer);
            if (window != 0)
            {
                _window = new NativeHandleOwner(window, _threadAffinity, _api.DestroyWindow);
            }

            if (renderer != 0)
            {
                _renderer = new NativeHandleOwner(renderer, _threadAffinity, _api.DestroyRenderer);
            }

            if (!created || _window is null || _renderer is null)
            {
                return FailInitialization("SDL_CreateWindowAndRenderer");
            }

            nint logicalTarget = _api.CreateLogicalTarget(_renderer.Handle, LogicalTargetSize);
            if (logicalTarget == 0)
            {
                return FailInitialization("SDL_CreateTexture(logical target)");
            }

            _logicalTarget = new NativeHandleOwner(logicalTarget, _threadAffinity, _api.DestroyTexture);
            Surface = surface;
            _hasRenderedFrame = false;
            IsInitialized = true;
            return true;
        }
        catch (Exception exception) when (IsNativeBoundaryException(exception))
        {
            return FailInitialization("SDL native binding load", exception);
        }
        catch (Exception exception)
        {
            CleanupAfterInitializationException(exception);
            throw;
        }
    }

    public void Resize(SurfaceDescriptor surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        EnsureReady();
        PixelSize windowSize = CalculateWindowSize(surface);
        Require(
            _api.SetWindowSize(_window!.Handle, windowSize.Width, windowSize.Height),
            "SDL_SetWindowSize");
        Surface = surface;
    }

    public void Render(RenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        EnsureReady();
        if (frame.LogicalSize != LogicalSize.VeloraDefault)
        {
            throw new ArgumentException(
                "The SDL parity backend currently requires the 1280x720 Velora logical surface.",
                nameof(frame));
        }

        _hasRenderedFrame = false;
        RenderLogicalScene(frame.Scene);
        ComposeToOutput();
        Require(_api.Present(_renderer!.Handle), "SDL_RenderPresent");
        _hasRenderedFrame = true;
    }

    public bool TryReadFrame(Span<byte> destination, out PixelSize size, out int bytesWritten)
    {
        bool succeeded = TryReadFrame(destination, out FrameReadbackResult readback);
        size = LogicalReadbackDescriptor.Size;
        bytesWritten = readback.BytesWritten;
        return succeeded;
    }

    public bool TryReadFrame(Span<byte> destination, out FrameReadbackResult readback)
    {
        EnsureReady();
        readback = new FrameReadbackResult(LogicalReadbackDescriptor, 0);
        if (!_hasRenderedFrame || destination.Length < LogicalReadbackDescriptor.RequiredBytes)
        {
            return false;
        }

        Require(
            _api.SetRenderTarget(_renderer!.Handle, _logicalTarget!.Handle),
            "SDL_SetRenderTarget(readback)");
        NativeReadbackResult result;
        try
        {
            result = _api.ReadPixelsBgra(_renderer.Handle, destination);
        }
        finally
        {
            Require(_api.SetRenderTarget(_renderer.Handle, 0), "SDL_SetRenderTarget(window)");
        }

        if (!result.IsSuccess)
        {
            throw new SdlRenderException("SDL_RenderReadPixels", result.Error);
        }

        if (result.Descriptor != LogicalReadbackDescriptor
            || result.BytesWritten != LogicalReadbackDescriptor.RequiredBytes)
        {
            throw new SdlRenderException(
                "SDL_RenderReadPixels",
                "SDL returned a readback that is not tightly packed, top-down BGRA8 "
                + $"at {LogicalTargetSize.Width}x{LogicalTargetSize.Height} ({result.BytesWritten} bytes).");
        }

        readback = new FrameReadbackResult(result.Descriptor, result.BytesWritten);
        return true;
    }

    /// <summary>Releases cached asset textures on the bound render thread.</summary>
    public void FlushTextureCache()
    {
        EnsureReady();
        DisposeTextureCache();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_threadAffinity.IsBound)
        {
            _threadAffinity.AssertCurrent();
        }

        try
        {
            ReleaseNativeResources(resetAffinity: false);
        }
        finally
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    internal static PixelSize CalculateWindowSize(SurfaceDescriptor surface)
    {
        double width = surface.DrawableSize.Width / surface.ScaleFactor;
        double height = surface.DrawableSize.Height / surface.ScaleFactor;
        int roundedWidth = Math.Max(1, checked((int)Math.Round(width, MidpointRounding.AwayFromZero)));
        int roundedHeight = Math.Max(1, checked((int)Math.Round(height, MidpointRounding.AwayFromZero)));
        return new PixelSize(roundedWidth, roundedHeight);
    }

    internal static byte CalculateAlpha(double alpha) => checked((byte)(alpha * byte.MaxValue));

    private void RenderLogicalScene(SceneRenderState scene)
    {
        Require(
            _api.SetRenderTarget(_renderer!.Handle, _logicalTarget!.Handle),
            "SDL_SetRenderTarget(logical target)");
        try
        {
            Require(_api.SetDrawColor(_renderer.Handle, _options.ClearColor), "SDL_SetRenderDrawColor");
            Require(_api.Clear(_renderer.Handle), "SDL_RenderClear(logical target)");
            RenderBackground(scene.Background);
            foreach (SpriteRenderItem sprite in scene.Sprites)
            {
                RenderSprite(sprite);
            }
        }
        finally
        {
            Require(_api.SetRenderTarget(_renderer.Handle, 0), "SDL_SetRenderTarget(window)");
        }
    }

    private void RenderBackground(BackgroundRenderState background)
    {
        if (background.TransitionProgress < 1)
        {
            RenderBackgroundLayer(
                background.PreviousAssetPath,
                byte.MaxValue);
        }

        if (background.TransitionProgress > 0)
        {
            RenderBackgroundLayer(
                background.CurrentAssetPath,
                CalculateAlpha(background.TransitionProgress));
        }
    }

    private void RenderBackgroundLayer(string? logicalPath, byte opacity)
    {
        if (string.IsNullOrWhiteSpace(logicalPath) || opacity == 0)
        {
            return;
        }

        TextureCacheEntry? texture = GetTexture(logicalPath);
        if (texture is null)
        {
            RenderProceduralBackground(logicalPath, opacity);
            return;
        }

        Require(_api.SetTextureAlpha(texture.Owner.Handle, opacity), "SDL_SetTextureAlphaMod(background)");
        Require(
            _api.DrawTexture(
                _renderer!.Handle,
                texture.Owner.Handle,
                new NativeRectF(0, 0, LogicalTargetSize.Width, LogicalTargetSize.Height),
                0,
                flipped: false),
            "SDL_RenderTexture(background)");
        Require(_api.SetTextureAlpha(texture.Owner.Handle, byte.MaxValue), "SDL_SetTextureAlphaMod(reset)");
    }

    private void RenderProceduralBackground(string logicalPath, byte opacity)
    {
        ProceduralBackgroundPalette palette = ProceduralBackground.CreatePalette(logicalPath);
        Require(_api.SetDrawBlendEnabled(_renderer!.Handle, enabled: true), "SDL_SetRenderDrawBlendMode(background)");
        try
        {
            for (int band = 0; band < ProceduralBackground.BandCount; band++)
            {
                ColorRgba color = ProceduralBackground.GetBandColor(palette, band, opacity);
                Require(_api.SetDrawColor(_renderer.Handle, color), "SDL_SetRenderDrawColor(background band)");
                Require(
                    _api.FillRect(
                        _renderer.Handle,
                        new NativeRectF(
                            0,
                            band * ProceduralBackground.BandHeight,
                            LogicalTargetSize.Width,
                            ProceduralBackground.BandHeight + 1)),
                    "SDL_RenderFillRect(background band)");
            }

            Require(
                _api.SetDrawColor(
                    _renderer.Handle,
                    new ColorRgba(172, 204, 157, checked((byte)(opacity / 10)))),
                "SDL_SetRenderDrawColor(background grid)");
            for (int x = 0; x < LogicalTargetSize.Width; x += ProceduralBackground.GridSpacing)
            {
                Require(
                    _api.DrawLine(_renderer.Handle, x, 0, x, LogicalTargetSize.Height),
                    "SDL_RenderLine(background grid)");
            }

            for (int y = 0; y < LogicalTargetSize.Height; y += ProceduralBackground.GridSpacing)
            {
                Require(
                    _api.DrawLine(_renderer.Handle, 0, y, LogicalTargetSize.Width, y),
                    "SDL_RenderLine(background grid)");
            }
        }
        finally
        {
            Require(
                _api.SetDrawBlendEnabled(_renderer.Handle, enabled: false),
                "SDL_SetRenderDrawBlendMode(reset)");
        }
    }

    private void RenderSprite(SpriteRenderItem sprite)
    {
        TextureCacheEntry? texture = GetTexture(sprite.AssetPath);
        if (texture is null)
        {
            return;
        }

        RectD bounds = LegacySpriteLayout.CalculateBounds(
            texture.Size,
            sprite,
            LogicalSize.VeloraDefault);
        Require(
            _api.SetTextureAlpha(texture.Owner.Handle, CalculateAlpha(sprite.Alpha)),
            "SDL_SetTextureAlphaMod(sprite)");
        Require(
            _api.DrawTexture(
                _renderer!.Handle,
                texture.Owner.Handle,
                NativeRectF.From(bounds),
                sprite.RotationDegrees,
                sprite.Flipped),
            "SDL_RenderTexture(sprite)");
        Require(_api.SetTextureAlpha(texture.Owner.Handle, byte.MaxValue), "SDL_SetTextureAlphaMod(reset)");
    }

    private void ComposeToOutput()
    {
        Require(_api.GetOutputSize(_renderer!.Handle, out PixelSize outputSize), "SDL_GetRenderOutputSize");
        LetterboxLayout layout = LetterboxLayout.Calculate(outputSize, LogicalSize.VeloraDefault);
        LastLayout = layout;
        Require(_api.SetDrawColor(_renderer.Handle, new ColorRgba(0, 0, 0)), "SDL_SetRenderDrawColor(letterbox)");
        Require(_api.Clear(_renderer.Handle), "SDL_RenderClear(window)");
        Require(_api.SetTextureAlpha(_logicalTarget!.Handle, byte.MaxValue), "SDL_SetTextureAlphaMod(logical target)");
        Require(
            _api.DrawTexture(
                _renderer.Handle,
                _logicalTarget.Handle,
                NativeRectF.From(layout.Viewport),
                0,
                flipped: false),
            "SDL_RenderTexture(logical target)");
    }

    private TextureCacheEntry? GetTexture(string logicalPath)
    {
        if (_textureCache.TryGetValue(logicalPath, out LinkedListNode<TextureCacheRecord>? cached))
        {
            TouchCacheEntry(cached);
            return cached.Value.Texture;
        }

        if (!AssetId.TryCreate(logicalPath, out AssetId assetId, out AssetPathError error))
        {
            RecordTextureFailure(logicalPath, $"Invalid logical asset identifier ({error}).");
            return null;
        }

        TextureAssetReadResult sourceResult;
        try
        {
            sourceResult = _textureSource.OpenRead(assetId);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            RecordTextureFailure(logicalPath, exception.Message);
            return null;
        }

        if (!sourceResult.IsSuccess)
        {
            RecordTextureFailure(logicalPath, sourceResult.Error ?? "The texture asset could not be opened.");
            return null;
        }

        using Stream stream = sourceResult.Stream!;
        nint texture;
        try
        {
            texture = _api.LoadTexture(_renderer!.Handle, stream, _options.MaximumTextureBytes);
        }
        catch (Exception exception) when (
            exception is IOException or ArgumentException or ArgumentOutOfRangeException)
        {
            RecordTextureFailure(logicalPath, exception.Message);
            return null;
        }
        catch (Exception exception) when (IsNativeBoundaryException(exception))
        {
            throw new SdlRenderException("IMG_LoadTexture_IO", exception.Message, exception);
        }

        if (texture == 0)
        {
            RecordTextureFailure(logicalPath, _api.GetError());
            return null;
        }

        NativeHandleOwner owner = new(texture, _threadAffinity, _api.DestroyTexture);
        try
        {
            if (!_api.GetTextureSize(texture, out PixelSize size))
            {
                throw new SdlRenderException("SDL_GetTextureSize", _api.GetError());
            }

            long pixelCount = (long)size.Width * size.Height;
            if (size.Width > _options.MaximumTextureDimension
                || size.Height > _options.MaximumTextureDimension
                || pixelCount > _options.MaximumTexturePixels)
            {
                owner.Dispose();
                RecordTextureFailure(
                    logicalPath,
                    $"Decoded texture {size.Width}x{size.Height} exceeds the configured "
                    + $"{_options.MaximumTextureDimension}-pixel dimension or "
                    + $"{_options.MaximumTexturePixels}-pixel limit.");
                return null;
            }

            if (!_api.ConfigureLoadedTexture(texture))
            {
                throw new SdlRenderException("SDL_SetTextureBlendMode/ScaleMode", _api.GetError());
            }

            TextureCacheEntry entry = new(owner, size);
            AddCacheRecord(new TextureCacheRecord(logicalPath, entry, null));
            return entry;
        }
        catch
        {
            if (!owner.IsDisposed)
            {
                owner.Dispose();
            }

            throw;
        }
    }

    private void RecordTextureFailure(string logicalPath, string? message)
    {
        string detail = string.IsNullOrWhiteSpace(message) ? "Texture data is unavailable." : message;
        TextureLoadFailure failure = new(logicalPath, detail);
        AddCacheRecord(new TextureCacheRecord(logicalPath, null, failure));
    }

    private void AddCacheRecord(TextureCacheRecord record)
    {
        while (_textureCache.Count >= _options.TextureCacheCapacity)
        {
            EvictLeastRecentlyUsed();
        }

        LinkedListNode<TextureCacheRecord> node = _textureLru.AddLast(record);
        _textureCache.Add(record.LogicalPath, node);
    }

    private void TouchCacheEntry(LinkedListNode<TextureCacheRecord> node)
    {
        if (!ReferenceEquals(_textureLru.Last, node))
        {
            _textureLru.Remove(node);
            _textureLru.AddLast(node);
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        LinkedListNode<TextureCacheRecord>? node = _textureLru.First;
        if (node is null)
        {
            return;
        }

        _textureLru.RemoveFirst();
        _textureCache.Remove(node.Value.LogicalPath);
        node.Value.Texture?.Owner.Dispose();
    }

    private bool FailInitialization(
        string operation,
        Exception? exception = null,
        string? errorOverride = null)
    {
        string error = errorOverride ?? exception?.Message ?? _api.GetError();
        LastInitializationError = new SdlInitializationError(
            operation,
            string.IsNullOrWhiteSpace(error) ? "Unknown SDL error." : error);
        ReleaseNativeResources(resetAffinity: true);
        return false;
    }

    private void CleanupAfterInitializationException(Exception exception)
    {
        try
        {
            ReleaseNativeResources(resetAffinity: true);
        }
        catch (Exception cleanupException)
        {
            throw new AggregateException(
                "SDL initialization failed and native cleanup also reported an error.",
                exception,
                cleanupException);
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private void EnsureReady()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsInitialized)
        {
            throw new InvalidOperationException("The SDL rendering backend is not initialized.");
        }

        _threadAffinity.AssertCurrent();
    }

    private void Require(bool succeeded, string operation)
    {
        if (!succeeded)
        {
            throw new SdlRenderException(operation, _api.GetError());
        }
    }

    private void ReleaseNativeResources(bool resetAffinity)
    {
        Exception? firstException = null;
        try
        {
            AttemptCleanup(DisposeTextureCache, ref firstException);
            NativeHandleOwner? logicalTarget = _logicalTarget;
            _logicalTarget = null;
            if (logicalTarget is not null)
            {
                AttemptCleanup(logicalTarget.Dispose, ref firstException);
            }

            NativeHandleOwner? renderer = _renderer;
            _renderer = null;
            if (renderer is not null)
            {
                AttemptCleanup(renderer.Dispose, ref firstException);
            }

            NativeHandleOwner? window = _window;
            _window = null;
            if (window is not null)
            {
                AttemptCleanup(window.Dispose, ref firstException);
            }

            if (_videoInitialized)
            {
                _videoInitialized = false;
                AttemptCleanup(_api.ShutdownVideo, ref firstException);
            }
        }
        finally
        {
            IsInitialized = false;
            _hasRenderedFrame = false;
            Surface = null;
            LastLayout = null;
            if (resetAffinity)
            {
                _threadAffinity.Reset();
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    private void DisposeTextureCache()
    {
        Exception? firstException = null;
        while (_textureLru.First is LinkedListNode<TextureCacheRecord> node)
        {
            _textureLru.RemoveFirst();
            _textureCache.Remove(node.Value.LogicalPath);
            NativeHandleOwner? owner = node.Value.Texture?.Owner;
            if (owner is not null)
            {
                AttemptCleanup(owner.Dispose, ref firstException);
            }
        }

        _textureCache.Clear();
        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    private static void AttemptCleanup(Action cleanup, ref Exception? firstException)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            firstException ??= exception;
        }
    }

    private static bool IsNativeBoundaryException(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    private sealed record TextureCacheRecord(
        string LogicalPath,
        TextureCacheEntry? Texture,
        TextureLoadFailure? Failure);
}
