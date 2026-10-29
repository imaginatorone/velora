namespace Velora.Rendering;

public enum SurfaceOrientation
{
    Unspecified,
    Landscape,
    Portrait,
    LandscapeFlipped,
    PortraitFlipped,
}

[Flags]
public enum GraphicsCapabilities
{
    None = 0,
    TextureRendering = 1 << 0,
    RenderTargets = 1 << 1,
    Shaders = 1 << 2,
    PostProcessing = 1 << 3,
    Depth = 1 << 4,
    Perspective = 1 << 5,
    Readback = 1 << 6,
}

public sealed record SurfaceDescriptor
{
    public SurfaceDescriptor(
        PixelSize drawableSize,
        double scaleFactor = 1,
        InsetsD safeArea = default,
        SurfaceOrientation orientation = SurfaceOrientation.Unspecified)
    {
        if (!drawableSize.IsValid)
        {
            throw new ArgumentException("A valid drawable size is required.", nameof(drawableSize));
        }

        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scaleFactor),
                scaleFactor,
                "Surface scale factor must be finite and positive.");
        }

        DrawableSize = drawableSize;
        ScaleFactor = scaleFactor;
        SafeArea = safeArea;
        Orientation = orientation;
    }

    public PixelSize DrawableSize { get; }

    public double ScaleFactor { get; }

    public InsetsD SafeArea { get; }

    public SurfaceOrientation Orientation { get; }
}
