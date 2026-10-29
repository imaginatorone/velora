using System.Numerics;
using Velora.Rendering;

namespace Velora.UI.Tests;

public sealed class UiLayoutEngineTests
{
    [Fact]
    public void EqualZOrderPreservesScreenThenInsertionOrder()
    {
        UiDocument document = Document(
            new UiScreen("late", UiSurfaceKind.ProjectDefined, [Panel("late")], layer: 2),
            new UiScreen("early", UiSurfaceKind.ProjectDefined, [Panel("first"), Panel("second")], layer: 1));

        UiRenderPlan plan = UiLayoutEngine.Build(
            document,
            Viewport,
            ["late", "early"]);

        Assert.Equal(["first", "second", "late"], plan.Commands.Select(command => command.ElementId));
    }

    [Fact]
    public void ZIndexSortsWithinScreenIndependentlyOfInsertion()
    {
        UiPanelElement high = Panel("high", zIndex: 8);
        UiPanelElement low = Panel("low", zIndex: -2);
        UiDocument document = Document(new UiScreen("screen", UiSurfaceKind.ProjectDefined, [high, low]));

        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, "screen");

        Assert.Equal(["low", "high"], plan.Commands.Select(command => command.ElementId));
    }

    [Fact]
    public void ChildZIndexAccumulatesFromParent()
    {
        UiPanelElement nested = new(
            "parent",
            Fill(),
            "style",
            [Panel("child", zIndex: 2)],
            zIndex: 5);
        UiDocument document = Document(
            new UiScreen("screen", UiSurfaceKind.ProjectDefined, [Panel("sibling", zIndex: 6), nested]));

        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, "screen");

        Assert.Equal(["parent", "sibling", "child"], plan.Commands.Select(command => command.ElementId));
    }

    [Fact]
    public void HitTestReturnsTopmostEnabledAction()
    {
        UiPanelElement bottom = ActionPanel("bottom", 0, new UiCommandAction(UiCommand.Advance));
        UiPanelElement top = ActionPanel("top", 1, new UiCommandAction(UiCommand.OpenBacklog));
        UiDocument document = Document(new UiScreen("screen", UiSurfaceKind.ProjectDefined, [bottom, top]));

        UiHitTarget? hit = UiLayoutEngine.Build(document, Viewport, "screen").HitTest(new Vector2(20, 20));

        Assert.NotNull(hit);
        Assert.Equal("top", hit.ElementId);
        Assert.Equal(new UiCommandAction(UiCommand.OpenBacklog), hit.Action);
    }

    [Fact]
    public void DisabledAndInvisibleElementsDoNotHit()
    {
        UiPanelElement disabled = new(
            "disabled",
            Fill(),
            "style",
            action: new UiCommandAction(UiCommand.Advance),
            isEnabled: false);
        UiPanelElement hidden = new(
            "hidden",
            Fill(),
            "style",
            action: new UiCommandAction(UiCommand.Quit),
            isVisible: false);
        UiDocument document = Document(new UiScreen("screen", UiSurfaceKind.ProjectDefined, [disabled, hidden]));

        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, "screen");

        Assert.Single(plan.Commands);
        Assert.Empty(plan.HitTargets);
        Assert.Null(plan.HitTest(new Vector2(20, 20)));
    }

    [Fact]
    public void ClippedChildOnlyHitsInsideParent()
    {
        UiPanelElement child = new(
            "child",
            new UiLayout(
                UiLength.Logical(100),
                UiLength.Logical(100),
                offset: new Vector2(75, 0)),
            "style",
            action: new UiCommandAction(UiCommand.Advance));
        UiPanelElement parent = new(
            "parent",
            new UiLayout(UiLength.Logical(100), UiLength.Logical(100)),
            "style",
            [child],
            clipChildren: true);
        UiDocument document = Document(new UiScreen("screen", UiSurfaceKind.ProjectDefined, [parent]));
        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, "screen");

        Assert.NotNull(plan.HitTest(new Vector2(90, 20)));
        Assert.Null(plan.HitTest(new Vector2(125, 20)));
    }

    [Fact]
    public void ActiveScreenInputOrderCannotChangePlan()
    {
        UiDocument document = Document(
            new UiScreen("one", UiSurfaceKind.ProjectDefined, [Panel("one")], layer: 0),
            new UiScreen("two", UiSurfaceKind.ProjectDefined, [Panel("two")], layer: 0));

        UiRenderPlan first = UiLayoutEngine.Build(document, Viewport, ["two", "one"]);
        UiRenderPlan second = UiLayoutEngine.Build(document, Viewport, ["one", "two"]);

        Assert.Equal(
            first.Commands.Select(command => command.ElementId),
            second.Commands.Select(command => command.ElementId));
    }

    [Fact]
    public void RejectsUnknownScreenIds()
    {
        UiDocument document = Document(new UiScreen("known", UiSurfaceKind.ProjectDefined, []));

        Assert.Throws<KeyNotFoundException>(() => UiLayoutEngine.Build(document, Viewport, "missing"));
    }

    [Fact]
    public void RejectsDefaultViewport()
    {
        UiDocument document = Document(new UiScreen("screen", UiSurfaceKind.ProjectDefined, []));

        Assert.Throws<ArgumentException>(() => UiLayoutEngine.Build(document, default, "screen"));
    }

    private static UiViewport Viewport => new(LogicalSize.VeloraDefault);

    private static UiDocument Document(params UiScreen[] screens) => new(
        new UiTheme([new KeyValuePair<string, UiStyle>("style", new UiStyle())]),
        screens);

    private static UiPanelElement Panel(string id, int zIndex = 0) => new(id, Fill(), "style", zIndex: zIndex);

    private static UiPanelElement ActionPanel(string id, int zIndex, UiAction action) => new(
        id,
        new UiLayout(UiLength.Logical(100), UiLength.Logical(100)),
        "style",
        action: action,
        zIndex: zIndex);

    private static UiLayout Fill() => new(UiLength.Fraction(1), UiLength.Fraction(1));
}
