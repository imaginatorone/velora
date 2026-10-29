namespace Velora.UI;

public abstract record UiAction;

public enum UiCommand
{
    Advance,
    SubmitTextInput,
    ContinueGame,
    NewGame,
    Resume,
    QuickSave,
    QuickLoad,
    ToggleAuto,
    ToggleSkip,
    ToggleUi,
    OpenMainMenu,
    OpenSave,
    OpenLoad,
    OpenSettings,
    OpenBacklog,
    CloseOverlay,
    ApplySettings,
    Quit,
}

public sealed record UiCommandAction : UiAction
{
    public UiCommandAction(UiCommand command)
    {
        if (!Enum.IsDefined(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "The UI command is invalid.");
        }

        Command = command;
    }

    public UiCommand Command { get; }
}

public sealed record UiChoiceAction : UiAction
{
    public UiChoiceAction(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        Index = index;
    }

    public int Index { get; }
}

public enum UiSlotOperation
{
    Save,
    Load,
    Delete,
}

public sealed record UiSlotAction : UiAction
{
    public UiSlotAction(UiSlotOperation operation, int slot)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "The save-slot operation is invalid.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        Operation = operation;
        Slot = slot;
    }

    public UiSlotOperation Operation { get; }

    public int Slot { get; }
}

public enum UiScreenOperation
{
    Show,
    Hide,
    Toggle,
}

public sealed record UiScreenAction : UiAction
{
    public UiScreenAction(UiScreenOperation operation, string screenId)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "The screen operation is invalid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(screenId);
        Operation = operation;
        ScreenId = screenId;
    }

    public UiScreenOperation Operation { get; }

    public string ScreenId { get; }
}

public enum UiSettingOperation
{
    Decrease,
    Increase,
    Toggle,
    Cycle,
}

public sealed record UiSettingAction : UiAction
{
    public UiSettingAction(string settingKey, UiSettingOperation operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "The setting operation is invalid.");
        }

        SettingKey = settingKey;
        Operation = operation;
    }

    public string SettingKey { get; }

    public UiSettingOperation Operation { get; }
}

public sealed record UiCustomAction : UiAction
{
    public UiCustomAction(string name, string? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Argument = argument;
    }

    public string Name { get; }

    public string? Argument { get; }
}
