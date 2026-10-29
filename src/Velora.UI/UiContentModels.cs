using System.Collections.ObjectModel;
using System.Numerics;
using Velora.Rendering;

namespace Velora.UI;

public sealed record DialogueUiContent
{
    public DialogueUiContent(
        string speaker,
        string text,
        ColorRgba? speakerColor = null,
        int? visibleRuneCount = null)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(text);
        if (visibleRuneCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleRuneCount));
        }

        Speaker = speaker;
        Text = text;
        SpeakerColor = speakerColor;
        VisibleRuneCount = visibleRuneCount;
    }

    public string Speaker { get; }

    public string Text { get; }

    public ColorRgba? SpeakerColor { get; }

    public int? VisibleRuneCount { get; }

    public string VisibleText => VisibleRuneCount is int count ? RuneText.Take(Text, count) : Text;
}

public sealed record ChoiceUiItem
{
    public ChoiceUiItem(string text, bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        IsEnabled = isEnabled;
    }

    public string Text { get; }

    public bool IsEnabled { get; }
}

public sealed class ChoiceMenuUiContent
{
    private readonly ReadOnlyCollection<ChoiceUiItem> _choices;

    public ChoiceMenuUiContent(IEnumerable<ChoiceUiItem> choices, string? prompt = null)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ChoiceUiItem[] copy = choices.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A choice menu must contain at least one choice.", nameof(choices));
        }

        if (copy.Any(static choice => choice is null))
        {
            throw new ArgumentException("Choices cannot contain null items.", nameof(choices));
        }

        _choices = Array.AsReadOnly(copy);
        Prompt = prompt;
    }

    public IReadOnlyList<ChoiceUiItem> Choices => _choices;

    public string? Prompt { get; }
}

public sealed record TextInputUiContent
{
    public TextInputUiContent(string prompt, string value = "", int maxRunes = 256)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRunes);
        Prompt = prompt;
        Value = value;
        MaxRunes = maxRunes;
    }

    public string Prompt { get; }

    public string Value { get; }

    public int MaxRunes { get; }

    public string DisplayValue => RuneText.Take(Value, MaxRunes);
}

public sealed record MainMenuUiContent
{
    public MainMenuUiContent(
        string title,
        string subtitle = "A Velora story",
        string version = "",
        bool canContinue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(subtitle);
        ArgumentNullException.ThrowIfNull(version);
        Title = title;
        Subtitle = subtitle;
        Version = version;
        CanContinue = canContinue;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string Version { get; }

    public bool CanContinue { get; }
}

public sealed record SaveSlotUiContent
{
    public SaveSlotUiContent(
        int slot,
        bool isOccupied,
        string label = "",
        string preview = "",
        string timestampText = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(timestampText);
        Slot = slot;
        IsOccupied = isOccupied;
        Label = label;
        Preview = preview;
        TimestampText = timestampText;
    }

    public int Slot { get; }

    public bool IsOccupied { get; }

    public string Label { get; }

    public string Preview { get; }

    public string TimestampText { get; }
}

public enum SaveLoadUiMode
{
    Save,
    Load,
}

public sealed class SaveLoadUiContent
{
    private readonly ReadOnlyCollection<SaveSlotUiContent> _slots;

    public SaveLoadUiContent(
        SaveLoadUiMode mode,
        IEnumerable<SaveSlotUiContent> slots,
        int? selectedSlot = null,
        int page = 0,
        int pageSize = 9)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The save/load mode is invalid.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        if (page > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page offset exceeds the supported range.");
        }
        if (selectedSlot is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedSlot));
        }

        SaveSlotUiContent[] copy = slots.ToArray();
        if (copy.Any(static slot => slot is null))
        {
            throw new ArgumentException("Save slots cannot contain null items.", nameof(slots));
        }

        if (copy.Select(slot => slot.Slot).Distinct().Count() != copy.Length)
        {
            throw new ArgumentException("Save slot identifiers must be unique.", nameof(slots));
        }

        Mode = mode;
        _slots = Array.AsReadOnly(copy);
        SelectedSlot = selectedSlot;
        Page = page;
        PageSize = pageSize;
    }

    public SaveLoadUiMode Mode { get; }

    public IReadOnlyList<SaveSlotUiContent> Slots => _slots;

    public int? SelectedSlot { get; }

    public int Page { get; }

    public int PageSize { get; }

    public IEnumerable<SaveSlotUiContent> VisibleSlots => _slots
        .OrderBy(slot => slot.Slot)
        .Skip(Page * PageSize)
        .Take(PageSize);
}

public enum SettingUiKind
{
    Range,
    Toggle,
    Choice,
}

public sealed record SettingUiItem
{
    public SettingUiItem(string key, string label, string displayValue, SettingUiKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(displayValue);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The setting kind is invalid.");
        }

        Key = key;
        Label = label;
        DisplayValue = displayValue;
        Kind = kind;
    }

    public string Key { get; }

    public string Label { get; }

    public string DisplayValue { get; }

    public SettingUiKind Kind { get; }
}

public sealed class SettingsUiContent
{
    private readonly ReadOnlyCollection<SettingUiItem> _items;

    public SettingsUiContent(IEnumerable<SettingUiItem> items, bool hasPendingChanges = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        SettingUiItem[] copy = items.ToArray();
        if (copy.Any(static item => item is null))
        {
            throw new ArgumentException("Settings cannot contain null items.", nameof(items));
        }

        if (copy.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != copy.Length)
        {
            throw new ArgumentException("Setting keys must be unique and case-sensitive.", nameof(items));
        }

        _items = Array.AsReadOnly(copy);
        HasPendingChanges = hasPendingChanges;
    }

    public IReadOnlyList<SettingUiItem> Items => _items;

    public bool HasPendingChanges { get; }
}

public sealed record BacklogEntryUiContent
{
    public BacklogEntryUiContent(string speaker, string text, ColorRgba? speakerColor = null)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(text);
        Speaker = speaker;
        Text = text;
        SpeakerColor = speakerColor;
    }

    public string Speaker { get; }

    public string Text { get; }

    public ColorRgba? SpeakerColor { get; }
}

public sealed class BacklogUiContent
{
    private readonly ReadOnlyCollection<BacklogEntryUiContent> _entries;

    public BacklogUiContent(IEnumerable<BacklogEntryUiContent> entries, int firstVisibleEntry = 0)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(firstVisibleEntry);
        BacklogEntryUiContent[] copy = entries.ToArray();
        if (copy.Any(static entry => entry is null))
        {
            throw new ArgumentException("Backlog entries cannot contain null items.", nameof(entries));
        }

        _entries = Array.AsReadOnly(copy);
        FirstVisibleEntry = firstVisibleEntry;
    }

    public IReadOnlyList<BacklogEntryUiContent> Entries => _entries;

    public int FirstVisibleEntry { get; }
}

public sealed record QuickMenuUiContent(
    bool CanSave = true,
    bool CanLoad = true,
    bool IsAutoActive = false,
    bool IsSkipActive = false);

public sealed record HudItemUiContent
{
    public HudItemUiContent(string id, string text, UiAnchor anchor, Vector2 offset = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(anchor))
        {
            throw new ArgumentOutOfRangeException(nameof(anchor), anchor, "The HUD anchor is invalid.");
        }

        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        Id = id;
        Text = text;
        Anchor = anchor;
        Offset = offset;
    }

    public string Id { get; }

    public string Text { get; }

    public UiAnchor Anchor { get; }

    public Vector2 Offset { get; }
}

public sealed class HudUiContent
{
    private readonly ReadOnlyCollection<HudItemUiContent> _items;

    public HudUiContent(IEnumerable<HudItemUiContent> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        HudItemUiContent[] copy = items.ToArray();
        if (copy.Any(static item => item is null))
        {
            throw new ArgumentException("HUD items cannot contain null items.", nameof(items));
        }

        if (copy.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
        {
            throw new ArgumentException("HUD item identifiers must be unique and case-sensitive.", nameof(items));
        }

        _items = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<HudItemUiContent> Items => _items;
}

public sealed record DefaultUiContent
{
    public DefaultUiContent(
        MainMenuUiContent mainMenu,
        DialogueUiContent dialogue,
        ChoiceMenuUiContent choiceMenu,
        TextInputUiContent textInput,
        SaveLoadUiContent save,
        SaveLoadUiContent load,
        SettingsUiContent settings,
        BacklogUiContent backlog,
        QuickMenuUiContent quickMenu,
        HudUiContent hud)
    {
        ArgumentNullException.ThrowIfNull(mainMenu);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(choiceMenu);
        ArgumentNullException.ThrowIfNull(textInput);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(backlog);
        ArgumentNullException.ThrowIfNull(quickMenu);
        ArgumentNullException.ThrowIfNull(hud);
        if (save.Mode != SaveLoadUiMode.Save)
        {
            throw new ArgumentException("Save content must use Save mode.", nameof(save));
        }

        if (load.Mode != SaveLoadUiMode.Load)
        {
            throw new ArgumentException("Load content must use Load mode.", nameof(load));
        }

        MainMenu = mainMenu;
        Dialogue = dialogue;
        ChoiceMenu = choiceMenu;
        TextInput = textInput;
        Save = save;
        Load = load;
        Settings = settings;
        Backlog = backlog;
        QuickMenu = quickMenu;
        Hud = hud;
    }

    public MainMenuUiContent MainMenu { get; }

    public DialogueUiContent Dialogue { get; }

    public ChoiceMenuUiContent ChoiceMenu { get; }

    public TextInputUiContent TextInput { get; }

    public SaveLoadUiContent Save { get; }

    public SaveLoadUiContent Load { get; }

    public SettingsUiContent Settings { get; }

    public BacklogUiContent Backlog { get; }

    public QuickMenuUiContent QuickMenu { get; }

    public HudUiContent Hud { get; }

    public static DefaultUiContent CreatePreview(string title = "Velora Story")
    {
        SaveSlotUiContent[] slots =
        [
            new(1, true, "Chapter 1", "A new beginning", "2026-08-22 12:00"),
            new(2, false),
            new(3, false),
        ];
        SettingUiItem[] settings =
        [
            new("text_speed", "Text Speed", "30", SettingUiKind.Range),
            new("auto_delay", "Auto Delay", "1.5 s", SettingUiKind.Range),
            new("music_volume", "Music Volume", "80%", SettingUiKind.Range),
            new("sound_volume", "Sound Volume", "80%", SettingUiKind.Range),
            new("skip_effects", "Skip Effects", "On", SettingUiKind.Toggle),
            new("display_mode", "Display", "Windowed", SettingUiKind.Choice),
        ];
        return new DefaultUiContent(
            new MainMenuUiContent(title, canContinue: true),
            new DialogueUiContent("Mira", "Welcome to Velora.", new ColorRgba(172, 204, 157)),
            new ChoiceMenuUiContent([new ChoiceUiItem("Begin"), new ChoiceUiItem("Not yet")]),
            new TextInputUiContent("What is your name?"),
            new SaveLoadUiContent(SaveLoadUiMode.Save, slots),
            new SaveLoadUiContent(SaveLoadUiMode.Load, slots),
            new SettingsUiContent(settings),
            new BacklogUiContent([new BacklogEntryUiContent("Mira", "Welcome to Velora.")]),
            new QuickMenuUiContent(),
            new HudUiContent([new HudItemUiContent("chapter", "Chapter 1", UiAnchor.TopRight)]));
    }
}
