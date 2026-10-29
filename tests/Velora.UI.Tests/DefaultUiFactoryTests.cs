using System.Numerics;
using Velora.Rendering;

namespace Velora.UI.Tests;

public sealed class DefaultUiFactoryTests
{
    [Fact]
    public void DefaultDocumentContainsEveryRequiredGameSurface()
    {
        UiDocument document = DefaultUiFactory.CreateDocument(DefaultUiContent.CreatePreview());

        Assert.Equal(10, document.Screens.Count);
        Assert.Collection(
            document.Screens,
            screen => Assert.Equal(UiSurfaceKind.Hud, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.Dialogue, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.QuickMenu, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.ChoiceMenu, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.MainMenu, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.Save, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.Load, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.Settings, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.Backlog, screen.Kind),
            screen => Assert.Equal(UiSurfaceKind.TextInput, screen.Kind));
    }

    [Fact]
    public void DefaultThemeSupportsAccentAndFontCustomization()
    {
        ColorRgba accent = new(10, 20, 30);
        UiTheme theme = DefaultUiFactory.CreateTheme(accent, "game-font");

        Assert.Equal(accent, theme.GetStyle(DefaultUiStyleKeys.Dialogue).BorderColor);
        Assert.Equal("game-font", theme.GetStyle(DefaultUiStyleKeys.Body).FontFamily);
    }

    [Fact]
    public void CustomThemeFlowsIntoRenderPlan()
    {
        UiTheme theme = DefaultUiFactory.CreateTheme().WithStyle(
            DefaultUiStyleKeys.Dialogue,
            new UiStyle(backgroundColor: new ColorRgba(1, 2, 3)));
        UiDocument document = DefaultUiFactory.CreateDocument(DefaultUiContent.CreatePreview(), theme);

        UiPanelDrawCommand box = Assert.IsType<UiPanelDrawCommand>(
            UiLayoutEngine.Build(document, Viewport, DefaultUiScreenIds.Dialogue)
                .Commands.Single(command => command.ElementId == "dialogue.box"));

        Assert.Equal(new ColorRgba(1, 2, 3), box.Style.BackgroundColor);
    }

    [Fact]
    public void DialogueBuildsNameBoxAndRuneLimitedText()
    {
        DialogueUiContent content = new("Mira", "A😀B", new ColorRgba(9, 8, 7), visibleRuneCount: 2);
        UiDocument document = new(
            DefaultUiFactory.CreateTheme(),
            [DefaultUiFactory.CreateDialogue(content)]);
        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, DefaultUiScreenIds.Dialogue);

        UiTextDrawCommand speaker = Assert.IsType<UiTextDrawCommand>(
            plan.Commands.Single(command => command.ElementId == "dialogue.speaker"));
        UiTextDrawCommand body = Assert.IsType<UiTextDrawCommand>(
            plan.Commands.Single(command => command.ElementId == "dialogue.text"));
        Assert.Equal(new ColorRgba(9, 8, 7), speaker.TextColorOverride);
        Assert.Equal("A😀", body.Text);
    }

    [Fact]
    public void QuickMenuExposesEightActionsAndActiveStyles()
    {
        UiScreen screen = DefaultUiFactory.CreateQuickMenu(new QuickMenuUiContent(IsAutoActive: true));
        UiDocument document = new(DefaultUiFactory.CreateTheme(), [screen]);
        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, DefaultUiScreenIds.QuickMenu);

        Assert.Equal(8, plan.HitTargets.Count);
        UiPanelDrawCommand auto = Assert.IsType<UiPanelDrawCommand>(
            plan.Commands.Single(command => command.ElementId == "quick-menu.auto"));
        Assert.Equal(
            DefaultUiFactory.CreateTheme().GetStyle(DefaultUiStyleKeys.ButtonActive),
            auto.Style);
    }

    [Fact]
    public void SafeAreaMovesDefaultHudAndDialogue()
    {
        UiDocument document = DefaultUiFactory.CreateDocument(DefaultUiContent.CreatePreview());
        UiViewport viewport = new(LogicalSize.VeloraDefault, new InsetsD(80, 30, 120, 40));

        UiRenderPlan hud = UiLayoutEngine.Build(document, viewport, DefaultUiScreenIds.Hud);
        UiRenderPlan dialogue = UiLayoutEngine.Build(document, viewport, DefaultUiScreenIds.Dialogue);

        Assert.Equal(980, hud.Commands.Single().Bounds.X, precision: 8);
        Assert.Equal(644, dialogue.Commands.Single(command => command.ElementId == "dialogue.box").Bounds.Bottom, precision: 8);
    }

    [Fact]
    public void DefaultDialogueHitReturnsAdvanceAction()
    {
        UiDocument document = DefaultUiFactory.CreateDocument(DefaultUiContent.CreatePreview());
        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, DefaultUiScreenIds.Dialogue);

        UiHitTarget? hit = plan.HitTest(new Vector2(640, 600));

        Assert.NotNull(hit);
        Assert.Equal(new UiCommandAction(UiCommand.Advance), hit.Action);
    }

    [Fact]
    public void ProjectImageButtonProducesImageCommandAndCustomAction()
    {
        UiImageButtonElement button = new(
            "notice.accept",
            "UI/Accept.PNG",
            new UiLayout(UiLength.Logical(120), UiLength.Logical(40)),
            DefaultUiStyleKeys.Button,
            new UiCustomAction("accept", "notice"));
        UiScreen screen = DefaultUiFactory.CreateProjectScreen("notice", [button]);
        UiDocument document = new(DefaultUiFactory.CreateTheme(), [screen]);

        UiRenderPlan plan = UiLayoutEngine.Build(document, Viewport, "notice");

        UiImageDrawCommand image = Assert.IsType<UiImageDrawCommand>(Assert.Single(plan.Commands));
        Assert.True(image.IsButton);
        Assert.Equal("UI/Accept.PNG", image.AssetPath);
        Assert.Equal(new UiCustomAction("accept", "notice"), Assert.Single(plan.HitTargets).Action);
    }

    [Fact]
    public void EveryDefaultSurfaceBuildsAConcreteRenderPlan()
    {
        UiDocument document = DefaultUiFactory.CreateDocument(DefaultUiContent.CreatePreview());

        foreach (UiScreen screen in document.Screens)
        {
            Assert.NotEmpty(UiLayoutEngine.Build(document, Viewport, screen.Id).Commands);
        }
    }

    private static UiViewport Viewport => new(LogicalSize.VeloraDefault);
}
