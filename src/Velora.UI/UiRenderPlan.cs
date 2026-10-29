using System.Collections.ObjectModel;
using System.Numerics;
using Velora.Rendering;

namespace Velora.UI;

public readonly record struct UiRenderOrder(int ScreenLayer, long ZIndex, long Sequence) : IComparable<UiRenderOrder>
{
    public static bool operator <(UiRenderOrder left, UiRenderOrder right) => left.CompareTo(right) < 0;

    public static bool operator <=(UiRenderOrder left, UiRenderOrder right) => left.CompareTo(right) <= 0;

    public static bool operator >(UiRenderOrder left, UiRenderOrder right) => left.CompareTo(right) > 0;

    public static bool operator >=(UiRenderOrder left, UiRenderOrder right) => left.CompareTo(right) >= 0;

    public int CompareTo(UiRenderOrder other)
    {
        int layer = ScreenLayer.CompareTo(other.ScreenLayer);
        if (layer != 0)
        {
            return layer;
        }

        int z = ZIndex.CompareTo(other.ZIndex);
        return z != 0 ? z : Sequence.CompareTo(other.Sequence);
    }
}

public abstract record UiDrawCommand(
    string ElementId,
    RectD Bounds,
    RectD ClipBounds,
    UiRenderOrder Order,
    UiStyle Style);

public sealed record UiPanelDrawCommand(
    string ElementId,
    RectD Bounds,
    RectD ClipBounds,
    UiRenderOrder Order,
    UiStyle Style)
    : UiDrawCommand(ElementId, Bounds, ClipBounds, Order, Style);

public sealed record UiTextDrawCommand(
    string ElementId,
    RectD Bounds,
    RectD ClipBounds,
    UiRenderOrder Order,
    UiStyle Style,
    string Text,
    bool Wrap,
    ColorRgba? TextColorOverride)
    : UiDrawCommand(ElementId, Bounds, ClipBounds, Order, Style);

public sealed record UiImageDrawCommand(
    string ElementId,
    RectD Bounds,
    RectD ClipBounds,
    UiRenderOrder Order,
    UiStyle Style,
    string AssetPath,
    bool IsButton)
    : UiDrawCommand(ElementId, Bounds, ClipBounds, Order, Style);

public sealed record UiHitTarget(
    string ElementId,
    RectD Bounds,
    RectD ClipBounds,
    UiRenderOrder Order,
    UiAction Action);

public sealed class UiRenderPlan
{
    private readonly ReadOnlyCollection<UiDrawCommand> _commands;
    private readonly ReadOnlyCollection<UiHitTarget> _hitTargets;

    internal UiRenderPlan(IEnumerable<UiDrawCommand> commands, IEnumerable<UiHitTarget> hitTargets)
    {
        _commands = Array.AsReadOnly(commands.OrderBy(command => command.Order).ToArray());
        _hitTargets = Array.AsReadOnly(hitTargets.OrderBy(target => target.Order).ToArray());
    }

    public IReadOnlyList<UiDrawCommand> Commands => _commands;

    public IReadOnlyList<UiHitTarget> HitTargets => _hitTargets;

    public UiHitTarget? HitTest(Vector2 logicalPoint)
    {
        if (!float.IsFinite(logicalPoint.X) || !float.IsFinite(logicalPoint.Y))
        {
            return null;
        }

        for (int index = _hitTargets.Count - 1; index >= 0; index--)
        {
            UiHitTarget target = _hitTargets[index];
            if (target.Bounds.Contains(logicalPoint) && target.ClipBounds.Contains(logicalPoint))
            {
                return target;
            }
        }

        return null;
    }
}
