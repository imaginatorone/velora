using Velora.Rendering;

namespace Velora.UI;

public static class UiLayoutEngine
{
    public static UiRenderPlan Build(
        UiDocument document,
        UiViewport viewport,
        IEnumerable<string> activeScreenIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(activeScreenIds);
        if (!viewport.LogicalSize.IsValid)
        {
            throw new ArgumentException("A valid UI viewport is required.", nameof(viewport));
        }

        HashSet<string> active = new(StringComparer.Ordinal);
        foreach (string id in activeScreenIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (!document.TryGetScreen(id, out _))
            {
                throw new KeyNotFoundException($"UI screen '{id}' is not defined.");
            }

            active.Add(id);
        }
        List<UiDrawCommand> commands = [];
        List<UiHitTarget> hitTargets = [];
        long sequence = 0;

        foreach (UiScreen screen in document.Screens
                     .Select((screen, index) => (screen, index))
                     .Where(item => active.Contains(item.screen.Id))
                     .OrderBy(item => item.screen.Layer)
                     .ThenBy(item => item.index)
                     .Select(item => item.screen))
        {
            foreach (UiElement element in screen.Elements)
            {
                AddElement(
                    element,
                    viewport.Bounds,
                    viewport.Bounds,
                    viewport,
                    document.Theme,
                    screen.Layer,
                    parentZ: 0,
                    commands,
                    hitTargets,
                    ref sequence);
            }
        }

        return new UiRenderPlan(commands, hitTargets);
    }

    public static UiRenderPlan Build(UiDocument document, UiViewport viewport, string activeScreenId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeScreenId);
        return Build(document, viewport, [activeScreenId]);
    }

    private static void AddElement(
        UiElement element,
        RectD parent,
        RectD clip,
        UiViewport viewport,
        UiTheme theme,
        int screenLayer,
        long parentZ,
        List<UiDrawCommand> commands,
        List<UiHitTarget> hitTargets,
        ref long sequence)
    {
        if (!element.IsVisible)
        {
            return;
        }

        RectD bounds = element.Layout.Resolve(parent, viewport);
        RectD visibleBounds = Intersect(bounds, clip);
        if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
        {
            return;
        }

        long effectiveZ = parentZ + element.ZIndex;
        UiRenderOrder order = new(screenLayer, effectiveZ, sequence++);
        UiStyle style = theme.GetStyle(element.StyleKey);
        commands.Add(element switch
        {
            UiPanelElement => new UiPanelDrawCommand(element.Id, bounds, clip, order, style),
            UiTextElement text => new UiTextDrawCommand(
                element.Id,
                bounds,
                clip,
                order,
                style,
                text.Text,
                text.Wrap,
                text.TextColorOverride),
            UiImageElement image => new UiImageDrawCommand(element.Id, bounds, clip, order, style, image.AssetPath, false),
            UiImageButtonElement button => new UiImageDrawCommand(element.Id, bounds, clip, order, style, button.AssetPath, true),
            _ => throw new InvalidOperationException($"Unsupported UI element type: {element.GetType().Name}."),
        });

        if (element.IsEnabled && element.Action is not null)
        {
            hitTargets.Add(new UiHitTarget(element.Id, bounds, clip, order, element.Action));
        }

        RectD childParent = element.Layout.ContentBounds(bounds);
        RectD childClip = element is UiPanelElement { ClipChildren: true }
            ? Intersect(clip, bounds)
            : clip;
        foreach (UiElement child in element.Children)
        {
            AddElement(
                child,
                childParent,
                childClip,
                viewport,
                theme,
                screenLayer,
                effectiveZ,
                commands,
                hitTargets,
                ref sequence);
        }
    }

    private static RectD Intersect(RectD first, RectD second)
    {
        double left = Math.Max(first.X, second.X);
        double top = Math.Max(first.Y, second.Y);
        double right = Math.Min(first.Right, second.Right);
        double bottom = Math.Min(first.Bottom, second.Bottom);
        return new RectD(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
