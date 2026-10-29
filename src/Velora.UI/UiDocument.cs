using System.Collections.ObjectModel;

namespace Velora.UI;

public enum UiSurfaceKind
{
    Dialogue,
    ChoiceMenu,
    TextInput,
    MainMenu,
    Save,
    Load,
    Settings,
    Backlog,
    QuickMenu,
    Hud,
    ProjectDefined,
}

public sealed class UiScreen
{
    private readonly ReadOnlyCollection<UiElement> _elements;

    public UiScreen(
        string id,
        UiSurfaceKind kind,
        IEnumerable<UiElement> elements,
        int layer = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(elements);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The UI surface kind is invalid.");
        }

        Id = id;
        Kind = kind;
        Layer = layer;
        _elements = Array.AsReadOnly(elements.ToArray());
        ValidateElementIds(_elements);
    }

    public string Id { get; }

    public UiSurfaceKind Kind { get; }

    public int Layer { get; }

    public IReadOnlyList<UiElement> Elements => _elements;

    private static void ValidateElementIds(IEnumerable<UiElement> elements)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        Stack<UiElement> remaining = new(elements.Reverse());
        while (remaining.TryPop(out UiElement? element))
        {
            ArgumentNullException.ThrowIfNull(element);
            if (!ids.Add(element.Id))
            {
                throw new ArgumentException($"Duplicate UI element id '{element.Id}'.", nameof(elements));
            }

            for (int index = element.Children.Count - 1; index >= 0; index--)
            {
                remaining.Push(element.Children[index]);
            }
        }
    }
}

public sealed class UiDocument
{
    private readonly ReadOnlyCollection<UiScreen> _screens;
    private readonly ReadOnlyDictionary<string, UiScreen> _screensById;

    public UiDocument(UiTheme theme, IEnumerable<UiScreen> screens)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(screens);
        Theme = theme;

        UiScreen[] copy = screens.ToArray();
        Dictionary<string, UiScreen> byId = new(StringComparer.Ordinal);
        foreach (UiScreen screen in copy)
        {
            ArgumentNullException.ThrowIfNull(screen);
            if (!byId.TryAdd(screen.Id, screen))
            {
                throw new ArgumentException($"Duplicate UI screen id '{screen.Id}'.", nameof(screens));
            }

            ValidateStyles(screen.Elements, theme);
        }

        _screens = Array.AsReadOnly(copy);
        _screensById = new ReadOnlyDictionary<string, UiScreen>(byId);
    }

    public UiTheme Theme { get; }

    public IReadOnlyList<UiScreen> Screens => _screens;

    public UiScreen GetScreen(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _screensById.TryGetValue(id, out UiScreen? screen)
            ? screen
            : throw new KeyNotFoundException($"UI screen '{id}' is not defined.");
    }

    public bool TryGetScreen(string id, out UiScreen? screen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _screensById.TryGetValue(id, out screen);
    }

    public UiDocument WithTheme(UiTheme theme) => new(theme, _screens);

    public UiDocument WithScreen(UiScreen screen, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(screen);
        int existing = Array.FindIndex(_screens.ToArray(), candidate => candidate.Id == screen.Id);
        if (existing < 0)
        {
            return new UiDocument(Theme, [.. _screens, screen]);
        }

        if (!replace)
        {
            throw new ArgumentException($"UI screen '{screen.Id}' already exists.", nameof(screen));
        }

        UiScreen[] copy = _screens.ToArray();
        copy[existing] = screen;
        return new UiDocument(Theme, copy);
    }

    private static void ValidateStyles(IEnumerable<UiElement> elements, UiTheme theme)
    {
        Stack<UiElement> remaining = new(elements.Reverse());
        while (remaining.TryPop(out UiElement? element))
        {
            theme.GetStyle(element.StyleKey);
            for (int index = element.Children.Count - 1; index >= 0; index--)
            {
                remaining.Push(element.Children[index]);
            }
        }
    }
}
