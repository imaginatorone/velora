using System.Numerics;
using Velora.Rendering;

namespace Velora.UI;

public enum UiLengthUnit
{
    Logical,
    Fraction,
}

public readonly record struct UiLength
{
    private UiLength(double value, UiLengthUnit unit)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "UI lengths must be finite and non-negative.");
        }

        Value = value;
        Unit = unit;
    }

    public double Value { get; }

    public UiLengthUnit Unit { get; }

    public static UiLength Logical(double value) => new(value, UiLengthUnit.Logical);

    public static UiLength Fraction(double value) => new(value, UiLengthUnit.Fraction);

    public double Resolve(double available) => Unit switch
    {
        UiLengthUnit.Logical => Value,
        UiLengthUnit.Fraction => available * Value,
        _ => throw new InvalidOperationException($"Unsupported UI length unit: {Unit}."),
    };
}

public enum UiAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

public enum UiLayoutTarget
{
    Parent,
    SafeArea,
    Viewport,
}

public sealed record UiLayout
{
    public UiLayout(
        UiLength width,
        UiLength height,
        UiAnchor anchor = UiAnchor.TopLeft,
        Vector2 offset = default,
        Vector2 relativeOffset = default,
        InsetsD margin = default,
        InsetsD padding = default,
        UiLayoutTarget target = UiLayoutTarget.Parent)
    {
        if (!Enum.IsDefined(anchor))
        {
            throw new ArgumentOutOfRangeException(nameof(anchor), anchor, "The UI anchor is invalid.");
        }

        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "The UI layout target is invalid.");
        }

        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "UI offsets must be finite.");
        }

        if (!float.IsFinite(relativeOffset.X) || !float.IsFinite(relativeOffset.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeOffset),
                relativeOffset,
                "Relative UI offsets must be finite.");
        }

        Width = width;
        Height = height;
        Anchor = anchor;
        Offset = offset;
        RelativeOffset = relativeOffset;
        Margin = margin;
        Padding = padding;
        Target = target;
    }

    public UiLength Width { get; }

    public UiLength Height { get; }

    public UiAnchor Anchor { get; }

    public Vector2 Offset { get; }

    /// <summary>Offset expressed as a fraction of the selected target rectangle.</summary>
    public Vector2 RelativeOffset { get; }

    public InsetsD Margin { get; }

    public InsetsD Padding { get; }

    public UiLayoutTarget Target { get; }

    public RectD Resolve(RectD parent, UiViewport viewport)
    {
        if (!double.IsFinite(parent.X)
            || !double.IsFinite(parent.Y)
            || !double.IsFinite(parent.Width)
            || !double.IsFinite(parent.Height)
            || parent.Width < 0
            || parent.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parent), parent, "Parent bounds must be finite and non-negative.");
        }

        if (!viewport.LogicalSize.IsValid)
        {
            throw new ArgumentException("A valid UI viewport is required.", nameof(viewport));
        }

        RectD target = Target switch
        {
            UiLayoutTarget.Parent => parent,
            UiLayoutTarget.SafeArea => viewport.SafeBounds,
            UiLayoutTarget.Viewport => viewport.Bounds,
            _ => throw new InvalidOperationException($"Unsupported UI layout target: {Target}."),
        };

        RectD available = Inset(target, Margin);
        double width = Width.Resolve(available.Width);
        double height = Height.Resolve(available.Height);
        (double xFactor, double yFactor) = Anchor switch
        {
            UiAnchor.TopLeft => (0, 0),
            UiAnchor.TopCenter => (0.5, 0),
            UiAnchor.TopRight => (1, 0),
            UiAnchor.CenterLeft => (0, 0.5),
            UiAnchor.Center => (0.5, 0.5),
            UiAnchor.CenterRight => (1, 0.5),
            UiAnchor.BottomLeft => (0, 1),
            UiAnchor.BottomCenter => (0.5, 1),
            UiAnchor.BottomRight => (1, 1),
            _ => throw new InvalidOperationException($"Unsupported UI anchor: {Anchor}."),
        };

        return new RectD(
            available.X + ((available.Width - width) * xFactor) + (available.Width * RelativeOffset.X) + Offset.X,
            available.Y + ((available.Height - height) * yFactor) + (available.Height * RelativeOffset.Y) + Offset.Y,
            width,
            height);
    }

    public RectD ContentBounds(RectD bounds) => Inset(bounds, Padding);

    private static RectD Inset(RectD bounds, InsetsD insets) => new(
        bounds.X + insets.Left,
        bounds.Y + insets.Top,
        Math.Max(0, bounds.Width - insets.Left - insets.Right),
        Math.Max(0, bounds.Height - insets.Top - insets.Bottom));
}

public readonly record struct UiViewport
{
    public UiViewport(LogicalSize logicalSize, InsetsD safeArea = default)
    {
        if (!logicalSize.IsValid)
        {
            throw new ArgumentException("A valid logical size is required.", nameof(logicalSize));
        }

        if (safeArea.Left + safeArea.Right >= logicalSize.Width
            || safeArea.Top + safeArea.Bottom >= logicalSize.Height)
        {
            throw new ArgumentException("Safe-area insets must leave a positive logical viewport.", nameof(safeArea));
        }

        LogicalSize = logicalSize;
        SafeArea = safeArea;
    }

    public LogicalSize LogicalSize { get; }

    public InsetsD SafeArea { get; }

    public RectD Bounds => new(0, 0, LogicalSize.Width, LogicalSize.Height);

    public RectD SafeBounds => new(
        SafeArea.Left,
        SafeArea.Top,
        LogicalSize.Width - SafeArea.Left - SafeArea.Right,
        LogicalSize.Height - SafeArea.Top - SafeArea.Bottom);

    public static UiViewport FromSurface(SurfaceDescriptor surface, LogicalSize logicalSize)
    {
        ArgumentNullException.ThrowIfNull(surface);
        LetterboxLayout letterbox = LetterboxLayout.Calculate(surface.DrawableSize, logicalSize);
        RectD viewport = letterbox.Viewport;
        double safeRight = surface.DrawableSize.Width - surface.SafeArea.Right;
        double safeBottom = surface.DrawableSize.Height - surface.SafeArea.Bottom;

        InsetsD logicalInsets = new(
            Math.Max(0, (surface.SafeArea.Left - viewport.X) / letterbox.Scale),
            Math.Max(0, (surface.SafeArea.Top - viewport.Y) / letterbox.Scale),
            Math.Max(0, (viewport.Right - safeRight) / letterbox.Scale),
            Math.Max(0, (viewport.Bottom - safeBottom) / letterbox.Scale));
        return new UiViewport(logicalSize, logicalInsets);
    }
}
