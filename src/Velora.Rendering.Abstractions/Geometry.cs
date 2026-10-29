using System.Numerics;

namespace Velora.Rendering;

public readonly record struct PixelSize
{
    public PixelSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsValid => Width > 0 && Height > 0;

    public double AspectRatio => (double)Width / Height;
}

public readonly record struct LogicalSize
{
    public LogicalSize(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Logical width must be finite and positive.");
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Logical height must be finite and positive.");
        }

        Width = width;
        Height = height;
    }

    public int WidthAsInt => checked((int)Width);

    public int HeightAsInt => checked((int)Height);

    public double Width { get; }

    public double Height { get; }

    public bool IsValid => Width > 0 && Height > 0;

    public double AspectRatio => Width / Height;

    public static LogicalSize VeloraDefault { get; } = new(1280, 720);
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
}

public readonly record struct InsetsD
{
    public InsetsD(double left, double top, double right, double bottom)
    {
        Validate(left, nameof(left));
        Validate(top, nameof(top));
        Validate(right, nameof(right));
        Validate(bottom, nameof(bottom));
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public double Left { get; }

    public double Top { get; }

    public double Right { get; }

    public double Bottom { get; }

    public static InsetsD Zero => default;

    private static void Validate(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Insets must be finite and non-negative.");
        }
    }
}
