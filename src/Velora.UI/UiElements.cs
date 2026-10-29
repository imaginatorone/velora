using System.Collections.ObjectModel;
using Velora.Rendering;

namespace Velora.UI;

public abstract class UiElement
{
    protected UiElement(
        string id,
        UiLayout layout,
        string styleKey,
        int zIndex,
        bool isVisible,
        bool isEnabled,
        UiAction? action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(styleKey);
        Id = id;
        Layout = layout;
        StyleKey = styleKey;
        ZIndex = zIndex;
        IsVisible = isVisible;
        IsEnabled = isEnabled;
        Action = action;
    }

    public string Id { get; }

    public UiLayout Layout { get; }

    public string StyleKey { get; }

    public int ZIndex { get; }

    public bool IsVisible { get; }

    public bool IsEnabled { get; }

    public UiAction? Action { get; }

    public virtual IReadOnlyList<UiElement> Children => Array.Empty<UiElement>();
}

public sealed class UiPanelElement : UiElement
{
    private readonly ReadOnlyCollection<UiElement> _children;

    public UiPanelElement(
        string id,
        UiLayout layout,
        string styleKey,
        IEnumerable<UiElement>? children = null,
        UiAction? action = null,
        int zIndex = 0,
        bool isVisible = true,
        bool isEnabled = true,
        bool clipChildren = false)
        : base(id, layout, styleKey, zIndex, isVisible, isEnabled, action)
    {
        UiElement[] copy = children?.ToArray() ?? [];
        if (copy.Any(static child => child is null))
        {
            throw new ArgumentException("UI panel children cannot contain null elements.", nameof(children));
        }

        _children = Array.AsReadOnly(copy);
        ClipChildren = clipChildren;
    }

    public override IReadOnlyList<UiElement> Children => _children;

    public bool ClipChildren { get; }
}

public sealed class UiTextElement : UiElement
{
    public UiTextElement(
        string id,
        string text,
        UiLayout layout,
        string styleKey,
        UiAction? action = null,
        int zIndex = 0,
        bool isVisible = true,
        bool isEnabled = true,
        bool wrap = true,
        ColorRgba? textColorOverride = null)
        : base(id, layout, styleKey, zIndex, isVisible, isEnabled, action)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Wrap = wrap;
        TextColorOverride = textColorOverride;
    }

    public string Text { get; }

    public bool Wrap { get; }

    public ColorRgba? TextColorOverride { get; }
}

public sealed class UiImageElement : UiElement
{
    public UiImageElement(
        string id,
        string assetPath,
        UiLayout layout,
        string styleKey,
        int zIndex = 0,
        bool isVisible = true)
        : base(id, layout, styleKey, zIndex, isVisible, true, null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        AssetPath = assetPath;
    }

    /// <summary>Case-sensitive logical asset path. Resolution belongs to Velora.Assets.</summary>
    public string AssetPath { get; }
}

public sealed class UiImageButtonElement : UiElement
{
    public UiImageButtonElement(
        string id,
        string assetPath,
        UiLayout layout,
        string styleKey,
        UiAction action,
        int zIndex = 0,
        bool isVisible = true,
        bool isEnabled = true)
        : base(id, layout, styleKey, zIndex, isVisible, isEnabled, action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        ArgumentNullException.ThrowIfNull(action);
        AssetPath = assetPath;
    }

    /// <summary>Case-sensitive logical asset path. Resolution belongs to Velora.Assets.</summary>
    public string AssetPath { get; }
}
