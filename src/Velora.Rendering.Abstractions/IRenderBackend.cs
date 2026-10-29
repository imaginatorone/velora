namespace Velora.Rendering;

public sealed record RenderFrame(
    SceneRenderState Scene,
    LogicalSize LogicalSize,
    TimeSpan PresentationTime);

public enum FramePixelFormat
{
    Bgra8 = 1,
}

public enum FrameRowOrder
{
    TopDown = 1,
}

/// <summary>Describes the exact byte layout returned by a frame readback.</summary>
public readonly record struct FrameReadbackDescriptor
{
    public FrameReadbackDescriptor(
        PixelSize size,
        FramePixelFormat pixelFormat,
        FrameRowOrder rowOrder,
        int rowPitch)
    {
        if (!size.IsValid)
        {
            throw new ArgumentException("A valid frame size is required.", nameof(size));
        }

        if (pixelFormat != FramePixelFormat.Bgra8)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, "Unsupported frame pixel format.");
        }

        if (rowOrder != FrameRowOrder.TopDown)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOrder), rowOrder, "Unsupported frame row order.");
        }

        int minimumRowPitch = checked(size.Width * BytesPerPixel);
        if (rowPitch < minimumRowPitch)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowPitch),
                rowPitch,
                $"Row pitch must be at least {minimumRowPitch} bytes.");
        }

        Size = size;
        PixelFormat = pixelFormat;
        RowOrder = rowOrder;
        RowPitch = rowPitch;
    }

    public const int BytesPerPixel = 4;

    public PixelSize Size { get; }

    public FramePixelFormat PixelFormat { get; }

    public FrameRowOrder RowOrder { get; }

    public int RowPitch { get; }

    public bool IsTightlyPacked => Size.IsValid && RowPitch == checked(Size.Width * BytesPerPixel);

    public int RequiredBytes => checked(RowPitch * Size.Height);

    public static FrameReadbackDescriptor CreateBgra8TopDown(PixelSize size) =>
        new(size, FramePixelFormat.Bgra8, FrameRowOrder.TopDown, checked(size.Width * BytesPerPixel));
}

public readonly record struct FrameReadbackResult
{
    public FrameReadbackResult(FrameReadbackDescriptor descriptor, int bytesWritten)
    {
        if (!descriptor.Size.IsValid)
        {
            throw new ArgumentException("A valid readback descriptor is required.", nameof(descriptor));
        }

        if (bytesWritten < 0 || bytesWritten > descriptor.RequiredBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytesWritten),
                bytesWritten,
                "Bytes written must fit within the described frame buffer.");
        }

        Descriptor = descriptor;
        BytesWritten = bytesWritten;
    }

    public FrameReadbackDescriptor Descriptor { get; }

    public int BytesWritten { get; }
}

public interface IRenderBackend : IDisposable
{
    bool IsInitialized { get; }

    GraphicsCapabilities Capabilities { get; }

    bool Initialize(SurfaceDescriptor surface);

    void Resize(SurfaceDescriptor surface);

    void Render(RenderFrame frame);

    bool TryReadFrame(Span<byte> destination, out PixelSize size, out int bytesWritten);

    /// <summary>
    /// Reads a frame and returns its explicit memory layout. The default implementation
    /// preserves compatibility for existing backends that implement the legacy overload.
    /// </summary>
    bool TryReadFrame(Span<byte> destination, out FrameReadbackResult readback)
    {
        bool succeeded = TryReadFrame(destination, out PixelSize size, out int bytesWritten);
        if (!size.IsValid)
        {
            readback = default;
            return false;
        }

        readback = new FrameReadbackResult(
            FrameReadbackDescriptor.CreateBgra8TopDown(size),
            bytesWritten);
        return succeeded;
    }
}
