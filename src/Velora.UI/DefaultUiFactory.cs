using System.Numerics;
using Velora.Rendering;

namespace Velora.UI;

public static class DefaultUiScreenIds
{
    public const string Dialogue = "dialogue";
    public const string ChoiceMenu = "choices";
    public const string TextInput = "text-input";
    public const string MainMenu = "main-menu";
    public const string Save = "save";
    public const string Load = "load";
    public const string Settings = "settings";
    public const string Backlog = "backlog";
    public const string QuickMenu = "quick-menu";
    public const string Hud = "hud";
}

public static class DefaultUiStyleKeys
{
    public const string Transparent = "transparent";
    public const string Overlay = "overlay";
    public const string Dialogue = "dialogue";
    public const string NameBox = "name-box";
    public const string Body = "body";
    public const string Speaker = "speaker";
    public const string Title = "title";
    public const string Subtitle = "subtitle";
    public const string Button = "button";
    public const string ButtonActive = "button-active";
    public const string ButtonDisabled = "button-disabled";
    public const string ButtonDanger = "button-danger";
    public const string ButtonText = "button-text";
    public const string MutedText = "muted-text";
    public const string Input = "input";
    public const string Slot = "slot";
    public const string SlotSelected = "slot-selected";
    public const string Hud = "hud";
}

public static class DefaultUiFactory
{
    private const int MaxVisibleBacklogEntries = 8;

    public static UiTheme CreateTheme(ColorRgba? accent = null, string fontFamily = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        ColorRgba accentColor = accent ?? new ColorRgba(172, 204, 157);
        KeyValuePair<string, UiStyle>[] styles =
        [
            Pair(DefaultUiStyleKeys.Transparent, new UiStyle(fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.Overlay,
                new UiStyle(backgroundColor: new ColorRgba(5, 3, 15, 235), fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.Dialogue,
                new UiStyle(
                    backgroundColor: new ColorRgba(10, 8, 20, 225),
                    borderColor: accentColor,
                    borderThickness: 1.5,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.NameBox,
                new UiStyle(
                    backgroundColor: new ColorRgba(24, 30, 28, 245),
                    borderColor: accentColor,
                    borderThickness: 1,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.Body,
                new UiStyle(
                    textColor: new ColorRgba(235, 235, 240),
                    fontFamily: fontFamily,
                    fontSize: 28,
                    verticalAlignment: UiVerticalAlignment.Top)),
            Pair(
                DefaultUiStyleKeys.Speaker,
                new UiStyle(
                    textColor: accentColor,
                    fontFamily: fontFamily,
                    fontSize: 24,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.Title,
                new UiStyle(
                    textColor: new ColorRgba(240, 248, 235),
                    fontFamily: fontFamily,
                    fontSize: 42,
                    textAlignment: UiTextAlignment.Center,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.Subtitle,
                new UiStyle(
                    textColor: accentColor,
                    fontFamily: fontFamily,
                    fontSize: 22,
                    textAlignment: UiTextAlignment.Center,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.Button,
                new UiStyle(
                    backgroundColor: new ColorRgba(28, 22, 48, 230),
                    borderColor: accentColor,
                    borderThickness: 1,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.ButtonActive,
                new UiStyle(
                    backgroundColor: new ColorRgba(35, 55, 35, 240),
                    borderColor: accentColor,
                    borderThickness: 2,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.ButtonDisabled,
                new UiStyle(
                    backgroundColor: new ColorRgba(20, 20, 28, 150),
                    borderColor: new ColorRgba(80, 80, 90, 100),
                    borderThickness: 1,
                    fontFamily: fontFamily,
                    opacity: 0.65)),
            Pair(
                DefaultUiStyleKeys.ButtonDanger,
                new UiStyle(
                    backgroundColor: new ColorRgba(48, 22, 28, 230),
                    borderColor: new ColorRgba(180, 100, 100),
                    borderThickness: 1,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.ButtonText,
                new UiStyle(
                    textColor: new ColorRgba(225, 220, 240),
                    fontFamily: fontFamily,
                    fontSize: 22,
                    textAlignment: UiTextAlignment.Center,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.MutedText,
                new UiStyle(
                    textColor: new ColorRgba(135, 140, 145, 210),
                    fontFamily: fontFamily,
                    fontSize: 18,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.Input,
                new UiStyle(
                    backgroundColor: new ColorRgba(220, 205, 175, 245),
                    borderColor: new ColorRgba(140, 110, 70),
                    borderThickness: 1,
                    textColor: new ColorRgba(30, 25, 15),
                    fontFamily: fontFamily,
                    fontSize: 24,
                    verticalAlignment: UiVerticalAlignment.Center)),
            Pair(
                DefaultUiStyleKeys.Slot,
                new UiStyle(
                    backgroundColor: new ColorRgba(18, 20, 28, 225),
                    borderColor: new ColorRgba(60, 70, 60, 180),
                    borderThickness: 1,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.SlotSelected,
                new UiStyle(
                    backgroundColor: new ColorRgba(35, 45, 30, 235),
                    borderColor: accentColor,
                    borderThickness: 2,
                    fontFamily: fontFamily)),
            Pair(
                DefaultUiStyleKeys.Hud,
                new UiStyle(
                    backgroundColor: new ColorRgba(10, 8, 20, 170),
                    borderColor: accentColor,
                    borderThickness: 1,
                    textColor: new ColorRgba(225, 225, 235),
                    fontFamily: fontFamily,
                    fontSize: 18,
                    textAlignment: UiTextAlignment.Center,
                    verticalAlignment: UiVerticalAlignment.Center)),
        ];
        return new UiTheme(styles);
    }

    public static UiDocument CreateDocument(DefaultUiContent content, UiTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        UiTheme resolvedTheme = theme ?? CreateTheme();
        return new UiDocument(
            resolvedTheme,
            [
                CreateHud(content.Hud),
                CreateDialogue(content.Dialogue),
                CreateQuickMenu(content.QuickMenu),
                CreateChoiceMenu(content.ChoiceMenu),
                CreateMainMenu(content.MainMenu),
                CreateSaveLoad(content.Save),
                CreateSaveLoad(content.Load),
                CreateSettings(content.Settings),
                CreateBacklog(content.Backlog),
                CreateTextInput(content.TextInput),
            ]);
    }

    public static UiScreen CreateDialogue(DialogueUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<UiElement> children =
        [
            new UiTextElement(
                "dialogue.text",
                content.VisibleText,
                FillLayout(),
                DefaultUiStyleKeys.Body),
        ];
        if (content.Speaker.Length > 0)
        {
            children.Add(
                new UiPanelElement(
                    "dialogue.name-box",
                    new UiLayout(
                        UiLength.Logical(280),
                        UiLength.Logical(42),
                        offset: new Vector2(0, -54)),
                    DefaultUiStyleKeys.NameBox,
                    [
                        new UiTextElement(
                            "dialogue.speaker",
                            content.Speaker,
                            FillLayout(),
                            DefaultUiStyleKeys.Speaker,
                            textColorOverride: content.SpeakerColor),
                    ],
                    zIndex: 1));
        }

        UiPanelElement dialogue = new(
            "dialogue.box",
            new UiLayout(
                UiLength.Fraction(1),
                UiLength.Logical(178),
                UiAnchor.BottomCenter,
                margin: new InsetsD(24, 0, 24, 36),
                padding: new InsetsD(30, 24, 30, 24),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Dialogue,
            children,
            new UiCommandAction(UiCommand.Advance));
        return new UiScreen(DefaultUiScreenIds.Dialogue, UiSurfaceKind.Dialogue, [dialogue], layer: 100);
    }

    public static UiScreen CreateChoiceMenu(ChoiceMenuUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        double panelHeight = Math.Min(
            600,
            72 + (content.Choices.Count * 58) + (string.IsNullOrEmpty(content.Prompt) ? 0 : 58));
        List<UiElement> children = [];
        double y = 0;
        if (!string.IsNullOrEmpty(content.Prompt))
        {
            children.Add(
                new UiTextElement(
                    "choices.prompt",
                    content.Prompt,
                    new UiLayout(UiLength.Fraction(1), UiLength.Logical(52)),
                    DefaultUiStyleKeys.Subtitle));
            y = 58;
        }

        for (int index = 0; index < content.Choices.Count; index++)
        {
            ChoiceUiItem choice = content.Choices[index];
            children.Add(
                TextButton(
                    $"choices.{index}",
                    choice.Text,
                    new UiLayout(
                        UiLength.Fraction(1),
                        UiLength.Logical(50),
                        offset: new Vector2(0, (float)(y + (index * 58)))),
                    new UiChoiceAction(index),
                    choice.IsEnabled));
        }

        UiPanelElement panel = new(
            "choices.panel",
            new UiLayout(
                UiLength.Fraction(0.62),
                UiLength.Logical(panelHeight),
                UiAnchor.Center,
                padding: new InsetsD(24, 24, 24, 24),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Dialogue,
            children,
            clipChildren: true);
        return new UiScreen(DefaultUiScreenIds.ChoiceMenu, UiSurfaceKind.ChoiceMenu, [panel], layer: 130);
    }

    public static UiScreen CreateTextInput(TextInputUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        UiPanelElement overlay = FullscreenOverlay("text-input.overlay", new UiCommandAction(UiCommand.CloseOverlay));
        UiPanelElement card = new(
            "text-input.card",
            new UiLayout(
                UiLength.Logical(520),
                UiLength.Logical(250),
                UiAnchor.Center,
                padding: new InsetsD(32, 22, 32, 22),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Dialogue,
            [
                new UiTextElement(
                    "text-input.title",
                    "TEXT INPUT",
                    new UiLayout(UiLength.Fraction(1), UiLength.Logical(34)),
                    DefaultUiStyleKeys.Subtitle),
                new UiTextElement(
                    "text-input.prompt",
                    content.Prompt,
                    new UiLayout(
                        UiLength.Fraction(1),
                        UiLength.Logical(48),
                        offset: new Vector2(0, 42)),
                    DefaultUiStyleKeys.Body),
                new UiTextElement(
                    "text-input.value",
                    content.DisplayValue + "|",
                    new UiLayout(
                        UiLength.Fraction(1),
                        UiLength.Logical(42),
                        offset: new Vector2(0, 104),
                        padding: new InsetsD(10, 4, 10, 4)),
                    DefaultUiStyleKeys.Input,
                    new UiCommandAction(UiCommand.SubmitTextInput),
                    wrap: false),
                TextButton(
                    "text-input.confirm",
                    "Confirm",
                    new UiLayout(
                        UiLength.Logical(150),
                        UiLength.Logical(42),
                        UiAnchor.BottomRight),
                    new UiCommandAction(UiCommand.SubmitTextInput)),
            ]);
        return new UiScreen(
            DefaultUiScreenIds.TextInput,
            UiSurfaceKind.TextInput,
            [overlay, card],
            layer: 300);
    }

    public static UiScreen CreateMainMenu(MainMenuUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<UiElement> controls =
        [
            new UiTextElement(
                "main-menu.title",
                content.Title,
                new UiLayout(UiLength.Fraction(0.5), UiLength.Logical(72)),
                DefaultUiStyleKeys.Title),
            new UiTextElement(
                "main-menu.subtitle",
                content.Subtitle,
                new UiLayout(
                    UiLength.Fraction(0.5),
                    UiLength.Logical(40),
                    offset: new Vector2(0, 72)),
                DefaultUiStyleKeys.Subtitle),
        ];

        (string Id, string Label, UiCommand Command, bool Enabled, bool Danger)[] buttons =
        [
            ("continue", "Continue", UiCommand.ContinueGame, content.CanContinue, false),
            ("new-game", "New Game", UiCommand.NewGame, true, false),
            ("saves", "Saves", UiCommand.OpenLoad, true, false),
            ("settings", "Settings", UiCommand.OpenSettings, true, false),
            ("quit", "Exit", UiCommand.Quit, true, true),
        ];
        for (int index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            controls.Add(
                TextButton(
                    $"main-menu.{button.Id}",
                    button.Label,
                    new UiLayout(
                        UiLength.Logical(320),
                        UiLength.Logical(50),
                        offset: new Vector2(0, 190 + (index * 58))),
                    new UiCommandAction(button.Command),
                    button.Enabled,
                    button.Danger));
        }

        if (content.Version.Length > 0)
        {
            controls.Add(
                new UiTextElement(
                    "main-menu.version",
                    content.Version,
                    new UiLayout(
                        UiLength.Logical(180),
                        UiLength.Logical(30),
                        UiAnchor.BottomRight),
                    DefaultUiStyleKeys.MutedText,
                    wrap: false));
        }

        UiPanelElement overlay = FullscreenOverlay("main-menu.overlay");
        UiPanelElement contentPanel = new(
            "main-menu.content",
            new UiLayout(
                UiLength.Fraction(1),
                UiLength.Fraction(1),
                padding: new InsetsD(48, 44, 48, 36),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Transparent,
            controls);
        return new UiScreen(
            DefaultUiScreenIds.MainMenu,
            UiSurfaceKind.MainMenu,
            [overlay, contentPanel],
            layer: 200);
    }

    public static UiScreen CreateSaveLoad(SaveLoadUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        bool saving = content.Mode == SaveLoadUiMode.Save;
        string screenId = saving ? DefaultUiScreenIds.Save : DefaultUiScreenIds.Load;
        UiSurfaceKind kind = saving ? UiSurfaceKind.Save : UiSurfaceKind.Load;
        UiSlotOperation operation = saving ? UiSlotOperation.Save : UiSlotOperation.Load;

        List<UiElement> children =
        [
            new UiTextElement(
                $"{screenId}.title",
                saving ? "Save" : "Load",
                new UiLayout(UiLength.Fraction(1), UiLength.Logical(54)),
                DefaultUiStyleKeys.Title),
        ];
        int row = 0;
        foreach (SaveSlotUiContent slot in content.VisibleSlots)
        {
            bool enabled = saving || slot.IsOccupied;
            string style = content.SelectedSlot == slot.Slot
                ? DefaultUiStyleKeys.SlotSelected
                : DefaultUiStyleKeys.Slot;
            string label = slot.IsOccupied ? slot.Label : "Empty Slot";
            string detail = slot.IsOccupied
                ? $"{RuneText.Truncate(slot.Preview, 60)}   {slot.TimestampText}".TrimEnd()
                : string.Empty;
            children.Add(
                new UiPanelElement(
                    $"{screenId}.slot.{slot.Slot}",
                    new UiLayout(
                        UiLength.Fraction(0.68),
                        UiLength.Logical(52),
                        offset: new Vector2(0, 66 + (row * 60))),
                    style,
                    [
                        new UiTextElement(
                            $"{screenId}.slot.{slot.Slot}.label",
                            $"{slot.Slot}.  {label}",
                            new UiLayout(UiLength.Fraction(1), UiLength.Logical(28)),
                            DefaultUiStyleKeys.ButtonText),
                        new UiTextElement(
                            $"{screenId}.slot.{slot.Slot}.detail",
                            detail,
                            new UiLayout(
                                UiLength.Fraction(1),
                                UiLength.Logical(20),
                                UiAnchor.BottomLeft),
                            DefaultUiStyleKeys.MutedText,
                            wrap: false),
                    ],
                    new UiSlotAction(operation, slot.Slot),
                    isEnabled: enabled,
                    clipChildren: true));
            row++;
        }

        children.Add(
            TextButton(
                $"{screenId}.back",
                "Back",
                new UiLayout(
                    UiLength.Logical(220),
                    UiLength.Logical(42),
                    UiAnchor.BottomRight),
                new UiCommandAction(UiCommand.CloseOverlay)));

        UiPanelElement contentPanel = new(
            $"{screenId}.content",
            new UiLayout(
                UiLength.Fraction(1),
                UiLength.Fraction(1),
                padding: new InsetsD(40, 20, 40, 24),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Transparent,
            children,
            clipChildren: true);
        return new UiScreen(
            screenId,
            kind,
            [FullscreenOverlay($"{screenId}.overlay"), contentPanel],
            layer: 210);
    }

    public static UiScreen CreateSettings(SettingsUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<UiElement> children =
        [
            new UiTextElement(
                "settings.title",
                "Settings",
                new UiLayout(UiLength.Fraction(1), UiLength.Logical(54)),
                DefaultUiStyleKeys.Title),
        ];
        for (int index = 0; index < content.Items.Count; index++)
        {
            SettingUiItem item = content.Items[index];
            float y = 72 + (index * 58);
            children.Add(
                new UiTextElement(
                    $"settings.{item.Key}.label",
                    item.Label,
                    new UiLayout(
                        UiLength.Fraction(0.42),
                        UiLength.Logical(42),
                        offset: new Vector2(0, y)),
                    DefaultUiStyleKeys.Body,
                    wrap: false));
            children.Add(
                new UiTextElement(
                    $"settings.{item.Key}.value",
                    item.DisplayValue,
                    new UiLayout(
                        UiLength.Fraction(0.2),
                        UiLength.Logical(42),
                        UiAnchor.TopCenter,
                        offset: new Vector2(0, y)),
                    DefaultUiStyleKeys.ButtonText,
                    wrap: false));

            UiSettingOperation left = item.Kind == SettingUiKind.Toggle
                ? UiSettingOperation.Toggle
                : UiSettingOperation.Decrease;
            UiSettingOperation right = item.Kind switch
            {
                SettingUiKind.Range => UiSettingOperation.Increase,
                SettingUiKind.Toggle => UiSettingOperation.Toggle,
                SettingUiKind.Choice => UiSettingOperation.Cycle,
                _ => throw new InvalidOperationException($"Unsupported setting kind: {item.Kind}."),
            };
            children.Add(
                TextButton(
                    $"settings.{item.Key}.previous",
                    item.Kind == SettingUiKind.Toggle ? "Toggle" : "−",
                    new UiLayout(
                        UiLength.Logical(item.Kind == SettingUiKind.Toggle ? 110 : 46),
                        UiLength.Logical(42),
                        UiAnchor.TopRight,
                        offset: new Vector2(item.Kind == SettingUiKind.Toggle ? -58 : -58, y)),
                    new UiSettingAction(item.Key, left)));
            if (item.Kind != SettingUiKind.Toggle)
            {
                children.Add(
                    TextButton(
                        $"settings.{item.Key}.next",
                        "+",
                        new UiLayout(
                            UiLength.Logical(46),
                            UiLength.Logical(42),
                            UiAnchor.TopRight,
                            offset: new Vector2(0, y)),
                        new UiSettingAction(item.Key, right)));
            }
        }

        children.Add(
            TextButton(
                "settings.apply",
                "Apply",
                new UiLayout(
                    UiLength.Logical(200),
                    UiLength.Logical(42),
                    UiAnchor.BottomLeft),
                new UiCommandAction(UiCommand.ApplySettings),
                content.HasPendingChanges));
        children.Add(
            TextButton(
                "settings.back",
                "Back",
                new UiLayout(
                    UiLength.Logical(200),
                    UiLength.Logical(42),
                    UiAnchor.BottomLeft,
                    offset: new Vector2(220, 0)),
                new UiCommandAction(UiCommand.CloseOverlay)));

        UiPanelElement panel = new(
            "settings.content",
            new UiLayout(
                UiLength.Fraction(0.82),
                UiLength.Fraction(0.9),
                UiAnchor.Center,
                padding: new InsetsD(44, 24, 44, 24),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Dialogue,
            children,
            clipChildren: true);
        return new UiScreen(
            DefaultUiScreenIds.Settings,
            UiSurfaceKind.Settings,
            [FullscreenOverlay("settings.overlay"), panel],
            layer: 210);
    }

    public static UiScreen CreateBacklog(BacklogUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<UiElement> children =
        [
            new UiTextElement(
                "backlog.title",
                "History",
                new UiLayout(UiLength.Fraction(1), UiLength.Logical(50)),
                DefaultUiStyleKeys.Title),
        ];
        int visible = 0;
        for (int index = content.FirstVisibleEntry;
             index < content.Entries.Count && visible < MaxVisibleBacklogEntries;
             index++, visible++)
        {
            BacklogEntryUiContent entry = content.Entries[index];
            float y = 60 + (visible * 68);
            children.Add(
                new UiTextElement(
                    $"backlog.{index}.speaker",
                    entry.Speaker,
                    new UiLayout(
                        UiLength.Logical(190),
                        UiLength.Logical(26),
                        offset: new Vector2(0, y)),
                    DefaultUiStyleKeys.Speaker,
                    wrap: false,
                    textColorOverride: entry.SpeakerColor));
            children.Add(
                new UiTextElement(
                    $"backlog.{index}.text",
                    RuneText.Truncate(entry.Text, 160),
                    new UiLayout(
                        UiLength.Fraction(0.8),
                        UiLength.Logical(58),
                        offset: new Vector2(200, y)),
                    DefaultUiStyleKeys.Body));
        }

        children.Add(
            TextButton(
                "backlog.close",
                "Close",
                new UiLayout(
                    UiLength.Logical(180),
                    UiLength.Logical(42),
                    UiAnchor.TopRight),
                new UiCommandAction(UiCommand.CloseOverlay)));
        UiPanelElement panel = new(
            "backlog.content",
            new UiLayout(
                UiLength.Fraction(1),
                UiLength.Fraction(1),
                padding: new InsetsD(40, 16, 40, 24),
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Transparent,
            children,
            clipChildren: true);
        return new UiScreen(
            DefaultUiScreenIds.Backlog,
            UiSurfaceKind.Backlog,
            [FullscreenOverlay("backlog.overlay", new UiCommandAction(UiCommand.CloseOverlay)), panel],
            layer: 250);
    }

    public static UiScreen CreateQuickMenu(QuickMenuUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        (string Id, string Label, UiCommand Command, bool Enabled, bool Active)[] items =
        [
            ("menu", "Menu", UiCommand.OpenMainMenu, true, false),
            ("save", "Save", UiCommand.QuickSave, content.CanSave, false),
            ("load", "Load", UiCommand.QuickLoad, content.CanLoad, false),
            ("auto", "Auto", UiCommand.ToggleAuto, true, content.IsAutoActive),
            ("skip", "Skip", UiCommand.ToggleSkip, true, content.IsSkipActive),
            ("history", "History", UiCommand.OpenBacklog, true, false),
            ("settings", "Settings", UiCommand.OpenSettings, true, false),
            ("hide", "Hide", UiCommand.ToggleUi, true, false),
        ];
        List<UiElement> children = [];
        for (int index = 0; index < items.Length; index++)
        {
            var item = items[index];
            children.Add(
                TextButton(
                    $"quick-menu.{item.Id}",
                    item.Label,
                    new UiLayout(
                        UiLength.Fraction(1d / items.Length),
                        UiLength.Fraction(1),
                        relativeOffset: new Vector2((float)index / items.Length, 0)),
                    new UiCommandAction(item.Command),
                    item.Enabled,
                    isActive: item.Active));
        }

        UiPanelElement panel = new(
            "quick-menu.bar",
            new UiLayout(
                UiLength.Fraction(1),
                UiLength.Logical(36),
                UiAnchor.BottomCenter,
                target: UiLayoutTarget.SafeArea),
            DefaultUiStyleKeys.Dialogue,
            children,
            clipChildren: true);
        return new UiScreen(DefaultUiScreenIds.QuickMenu, UiSurfaceKind.QuickMenu, [panel], layer: 110);
    }

    public static UiScreen CreateHud(HudUiContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        UiElement[] elements =
        [
            .. content.Items.Select(item => new UiTextElement(
                $"hud.{item.Id}",
                item.Text,
                new UiLayout(
                    UiLength.Logical(180),
                    UiLength.Logical(36),
                    item.Anchor,
                    item.Offset,
                    target: UiLayoutTarget.SafeArea),
                DefaultUiStyleKeys.Hud,
                wrap: false)),
        ];
        return new UiScreen(DefaultUiScreenIds.Hud, UiSurfaceKind.Hud, elements, layer: 40);
    }

    public static UiScreen CreateProjectScreen(string id, IEnumerable<UiElement> elements, int layer = 80)
        => new(id, UiSurfaceKind.ProjectDefined, elements, layer);

    private static UiPanelElement FullscreenOverlay(string id, UiAction? action = null) => new(
        id,
        new UiLayout(
            UiLength.Fraction(1),
            UiLength.Fraction(1),
            target: UiLayoutTarget.Viewport),
        DefaultUiStyleKeys.Overlay,
        action: action,
        zIndex: -100);

    private static UiPanelElement TextButton(
        string id,
        string text,
        UiLayout layout,
        UiAction action,
        bool isEnabled = true,
        bool isDanger = false,
        bool isActive = false)
    {
        string style = !isEnabled
            ? DefaultUiStyleKeys.ButtonDisabled
            : isDanger
                ? DefaultUiStyleKeys.ButtonDanger
                : isActive
                    ? DefaultUiStyleKeys.ButtonActive
                    : DefaultUiStyleKeys.Button;
        return new UiPanelElement(
            id,
            layout,
            style,
            [
                new UiTextElement(
                    $"{id}.label",
                    text,
                    FillLayout(),
                    DefaultUiStyleKeys.ButtonText),
            ],
            action,
            isEnabled: isEnabled,
            clipChildren: true);
    }

    private static UiLayout FillLayout() => new(UiLength.Fraction(1), UiLength.Fraction(1));

    private static KeyValuePair<string, UiStyle> Pair(string key, UiStyle value) => new(key, value);
}
