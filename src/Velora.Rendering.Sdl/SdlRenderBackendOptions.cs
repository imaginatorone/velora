using Velora.Rendering;

namespace Velora.Rendering.Sdl;

/// <summary>Configuration owned by an SDL Player or PreviewHost edge.</summary>
public sealed record SdlRenderBackendOptions
{
    private int _maximumTextureDimension = DefaultMaximumTextureDimension;
    private long _maximumTexturePixels = DefaultMaximumTexturePixels;
    private int _textureCacheCapacity = DefaultTextureCacheCapacity;

    public const int DefaultMaximumTextureBytes = 64 * 1024 * 1024;
    public const int DefaultMaximumTextureDimension = 8192;
    public const long DefaultMaximumTexturePixels = 32L * 1024 * 1024;
    public const int DefaultTextureCacheCapacity = 256;

    public SdlRenderBackendOptions(
        string windowTitle = "Velora",
        int maximumTextureBytes = DefaultMaximumTextureBytes,
        ColorRgba? clearColor = null,
        bool resizable = true,
        bool highPixelDensity = true,
        bool createHiddenWindow = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowTitle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTextureBytes);

        WindowTitle = windowTitle;
        MaximumTextureBytes = maximumTextureBytes;
        ClearColor = clearColor ?? new ColorRgba(16, 16, 24);
        Resizable = resizable;
        HighPixelDensity = highPixelDensity;
        CreateHiddenWindow = createHiddenWindow;
    }

    public string WindowTitle { get; }

    public int MaximumTextureBytes { get; }

    public ColorRgba ClearColor { get; }

    public bool Resizable { get; }

    public bool HighPixelDensity { get; }

    public bool CreateHiddenWindow { get; }

    public int MaximumTextureDimension
    {
        get => _maximumTextureDimension;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maximumTextureDimension = value;
        }
    }

    public long MaximumTexturePixels
    {
        get => _maximumTexturePixels;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maximumTexturePixels = value;
        }
    }

    public int TextureCacheCapacity
    {
        get => _textureCacheCapacity;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _textureCacheCapacity = value;
        }
    }
}
