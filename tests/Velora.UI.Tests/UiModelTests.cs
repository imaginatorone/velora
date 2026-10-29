using System.Numerics;
using Velora.Rendering;

namespace Velora.UI.Tests;

public sealed class UiModelTests
{
    [Fact]
    public void ScreenRejectsDuplicateNestedElementIds()
    {
        UiPanelElement parent = new(
            "duplicate",
            Fill(),
            "style",
            [new UiTextElement("duplicate", "text", Fill(), "style")]);

        Assert.Throws<ArgumentException>(
            () => new UiScreen("screen", UiSurfaceKind.ProjectDefined, [parent]));
    }

    [Fact]
    public void DocumentUsesCaseSensitiveScreenIds()
    {
        UiDocument document = Document(
            new UiScreen("Notice", UiSurfaceKind.ProjectDefined, []),
            new UiScreen("notice", UiSurfaceKind.ProjectDefined, []));

        Assert.NotSame(document.GetScreen("Notice"), document.GetScreen("notice"));
        Assert.False(document.TryGetScreen("NOTICE", out _));
    }

    [Fact]
    public void DocumentRejectsMissingStyle()
    {
        UiScreen screen = new(
            "screen",
            UiSurfaceKind.ProjectDefined,
            [new UiPanelElement("panel", Fill(), "missing")]);

        Assert.Throws<KeyNotFoundException>(() => Document(screen));
    }

    [Fact]
    public void ModelCopiesCallerCollections()
    {
        List<UiElement> children = [new UiTextElement("first", "A", Fill(), "style")];
        UiPanelElement panel = new("panel", Fill(), "style", children);
        children.Add(new UiTextElement("second", "B", Fill(), "style"));

        Assert.Single(panel.Children);
    }

    [Fact]
    public void LogicalAssetPathPreservesExactCaseAndSpelling()
    {
        const string path = "Images/UI/Continue.PNG";
        UiImageButtonElement button = new(
            "continue",
            path,
            Fill(),
            "style",
            new UiCustomAction("continue"));

        Assert.Equal(path, button.AssetPath);
    }

    [Theory]
    [InlineData(-1)]
    public void ActionsRejectInvalidIndices(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiChoiceAction(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiSlotAction(UiSlotOperation.Save, value));
    }

    [Fact]
    public void PublicUiModelsRejectUnknownEnumValues()
    {
        var invalid = (UiAnchor)int.MaxValue;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiLayout(UiLength.Logical(1), UiLength.Logical(1), invalid));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiScreen("screen", (UiSurfaceKind)int.MaxValue, []));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiStyle(textAlignment: (UiTextAlignment)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiCommandAction((UiCommand)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiSlotAction((UiSlotOperation)int.MaxValue, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiScreenAction((UiScreenOperation)int.MaxValue, "screen"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UiSettingAction("volume", (UiSettingOperation)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SaveLoadUiContent((SaveLoadUiMode)int.MaxValue, []));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SettingUiItem("volume", "Volume", "100%", (SettingUiKind)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HudItemUiContent("hud", "text", invalid));
    }

    [Fact]
    public void RuneTextDoesNotSplitSupplementaryCharacters()
    {
        const string text = "A😀B";

        Assert.Equal("A😀", RuneText.Take(text, 2));
        Assert.Equal("A😀…", RuneText.Truncate(text, 2));
    }

    [Fact]
    public void TextInputLimitUsesRunesInsteadOfUtf16Units()
    {
        TextInputUiContent content = new("Name", "😀😀x", maxRunes: 2);

        Assert.Equal("😀😀", content.DisplayValue);
    }

    [Fact]
    public void SaveModelRejectsDuplicateSlots()
    {
        SaveSlotUiContent slot = new(1, false);

        Assert.Throws<ArgumentException>(
            () => new SaveLoadUiContent(SaveLoadUiMode.Save, [slot, slot]));
    }

    [Fact]
    public void DocumentReplacementIsImmutable()
    {
        UiScreen original = new("screen", UiSurfaceKind.ProjectDefined, []);
        UiDocument document = Document(original);
        UiScreen replacement = new("screen", UiSurfaceKind.Hud, []);

        UiDocument changed = document.WithScreen(replacement, replace: true);

        Assert.Equal(UiSurfaceKind.ProjectDefined, document.GetScreen("screen").Kind);
        Assert.Equal(UiSurfaceKind.Hud, changed.GetScreen("screen").Kind);
    }

    [Fact]
    public void ThemeReplacementIsImmutable()
    {
        UiTheme theme = new([Pair("style", new UiStyle(fontSize: 18))]);
        UiTheme changed = theme.WithStyle("style", new UiStyle(fontSize: 30));

        Assert.Equal(18, theme.GetStyle("style").FontSize);
        Assert.Equal(30, changed.GetStyle("style").FontSize);
    }

    [Fact]
    public void SaveLoadPagingSortsSlotsDeterministically()
    {
        SaveLoadUiContent content = new(
            SaveLoadUiMode.Load,
            [new SaveSlotUiContent(3, true), new SaveSlotUiContent(1, true), new SaveSlotUiContent(2, true)],
            page: 1,
            pageSize: 2);

        Assert.Equal([3], content.VisibleSlots.Select(slot => slot.Slot));
    }

    private static UiDocument Document(params UiScreen[] screens) => new(
        new UiTheme([Pair("style", new UiStyle())]),
        screens);

    private static UiLayout Fill() => new(UiLength.Fraction(1), UiLength.Fraction(1));

    private static KeyValuePair<string, UiStyle> Pair(string key, UiStyle style) => new(key, style);
}
