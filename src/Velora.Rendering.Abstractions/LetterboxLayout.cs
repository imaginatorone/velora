using System.Numerics;

namespace Velora.Rendering;

public readonly record struct LetterboxLayout
{
    private LetterboxLayout(PixelSize output, LogicalSize logical, double scale, RectD viewport)
    {
        Output = output;
        Logical = logical;
        Scale = scale;
        Viewport = viewport;
    }

    public PixelSize Output { get; }

    public LogicalSize Logical { get; }

    public double Scale { get; }

    public RectD Viewport { get; }

    public static LetterboxLayout Calculate(PixelSize output, LogicalSize logical)
    {
        if (!output.IsValid)
        {
            throw new ArgumentException("A valid output size is required.", nameof(output));
        }

        if (!logical.IsValid)
        {
            throw new ArgumentException("A valid logical size is required.", nameof(logical));
        }

        double scale = Math.Min(output.Width / logical.Width, output.Height / logical.Height);
        double viewportWidth = logical.Width * scale;
        double viewportHeight = logical.Height * scale;
        RectD viewport = new(
            (output.Width - viewportWidth) / 2,
            (output.Height - viewportHeight) / 2,
            viewportWidth,
            viewportHeight);
        return new LetterboxLayout(output, logical, scale, viewport);
    }

    public Vector2 LogicalToOutput(Vector2 point) => new(
        (float)(Viewport.X + (point.X * Scale)),
        (float)(Viewport.Y + (point.Y * Scale)));

    public bool TryOutputToLogical(Vector2 point, out Vector2 logicalPoint)
    {
        if (!Viewport.Contains(point))
        {
            logicalPoint = default;
            return false;
        }

        logicalPoint = new Vector2(
            (float)((point.X - Viewport.X) / Scale),
            (float)((point.Y - Viewport.Y) / Scale));
        return true;
    }
}
