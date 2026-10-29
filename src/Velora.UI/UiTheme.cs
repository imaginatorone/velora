using System.Collections.ObjectModel;
using Velora.Rendering;

namespace Velora.UI;

public enum UiTextAlignment
{
    Start,
    Center,
    End,
}

public enum UiVerticalAlignment
{
    Top,
    Center,
    Bottom,
}

public enum UiImageFit
{
    Stretch,
    Contain,
    Cover,
}

public sealed record UiStyle
{
    public UiStyle(
        ColorRgba? backgroundColor = null,
        ColorRgba? borderColor = null,
        double borderThickness = 0,
        ColorRgba? textColor = null,
        string fontFamily = "default",
        double fontSize = 24,
        UiTextAlignment textAlignment = UiTextAlignment.Start,
        UiVerticalAlignment verticalAlignment = UiVerticalAlignment.Top,
        UiImageFit imageFit = UiImageFit.Contain,
        double opacity = 1)
    {
        if (!Enum.IsDefined(textAlignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textAlignment),
                textAlignment,
                "The text alignment is invalid.");
        }

        if (!Enum.IsDefined(verticalAlignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(verticalAlignment),
                verticalAlignment,
                "The vertical alignment is invalid.");
        }

        if (!Enum.IsDefined(imageFit))
        {
            throw new ArgumentOutOfRangeException(nameof(imageFit), imageFit, "The image fit mode is invalid.");
        }

        if (!double.IsFinite(borderThickness) || borderThickness < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(borderThickness),
                borderThickness,
                "Border thickness must be finite and non-negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        if (!double.IsFinite(fontSize) || fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be finite and positive.");
        }

        if (!double.IsFinite(opacity) || opacity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), opacity, "Opacity must be between zero and one.");
        }

        BackgroundColor = backgroundColor;
        BorderColor = borderColor;
        BorderThickness = borderThickness;
        TextColor = textColor ?? new ColorRgba(255, 255, 255);
        FontFamily = fontFamily;
        FontSize = fontSize;
        TextAlignment = textAlignment;
        VerticalAlignment = verticalAlignment;
        ImageFit = imageFit;
        Opacity = opacity;
    }

    public ColorRgba? BackgroundColor { get; }

    public ColorRgba? BorderColor { get; }

    public double BorderThickness { get; }

    public ColorRgba TextColor { get; }

    public string FontFamily { get; }

    public double FontSize { get; }

    public UiTextAlignment TextAlignment { get; }

    public UiVerticalAlignment VerticalAlignment { get; }

    public UiImageFit ImageFit { get; }

    public double Opacity { get; }
}

public sealed class UiTheme
{
    private readonly ReadOnlyDictionary<string, UiStyle> _styles;

    public UiTheme(IEnumerable<KeyValuePair<string, UiStyle>> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        Dictionary<string, UiStyle> copy = new(StringComparer.Ordinal);
        foreach ((string key, UiStyle style) in styles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(style);
            if (!copy.TryAdd(key, style))
            {
                throw new ArgumentException($"Duplicate UI style key '{key}'.", nameof(styles));
            }
        }

        if (copy.Count == 0)
        {
            throw new ArgumentException("A UI theme must define at least one style.", nameof(styles));
        }

        _styles = new ReadOnlyDictionary<string, UiStyle>(copy);
    }

    public IReadOnlyDictionary<string, UiStyle> Styles => _styles;

    public UiStyle GetStyle(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _styles.TryGetValue(key, out UiStyle? style)
            ? style
            : throw new KeyNotFoundException($"UI style '{key}' is not defined by the theme.");
    }

    public UiTheme WithStyle(string key, UiStyle style)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(style);
        Dictionary<string, UiStyle> copy = new(_styles, StringComparer.Ordinal)
        {
            [key] = style,
        };
        return new UiTheme(copy);
    }
}
