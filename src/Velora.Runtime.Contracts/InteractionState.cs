namespace Velora.Runtime.Contracts;

/// <summary>A single ordered choice visible to the player.</summary>
public sealed record ChoiceOption
{
    /// <summary>Creates a choice option.</summary>
    public ChoiceOption(int index, string text, bool isEnabled = true)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The choice index must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(text);
        Index = index;
        Text = text;
        IsEnabled = isEnabled;
    }

    public int Index { get; }

    public string Text { get; }

    public bool IsEnabled { get; }
}

/// <summary>Captures an active menu and its deterministic choice ordering.</summary>
public sealed record ChoiceState
{
    /// <summary>Creates a choice state whose option indices must be contiguous from zero.</summary>
    public ChoiceState(IEnumerable<ChoiceOption> options)
    {
        IReadOnlyList<ChoiceOption> copy = ContractCollections.CopyList(
            options,
            nameof(options),
            option => ArgumentNullException.ThrowIfNull(option));

        if (copy.Count == 0)
        {
            throw new ArgumentException("A pending choice must contain at least one option.", nameof(options));
        }

        for (int index = 0; index < copy.Count; index++)
        {
            if (copy[index].Index != index)
            {
                throw new ArgumentException(
                    "Choice option indices must be contiguous and ordered from zero.",
                    nameof(options));
            }
        }

        Options = copy;
    }

    public IReadOnlyList<ChoiceOption> Options { get; }
}

/// <summary>Captures an active free-text input request.</summary>
public sealed record TextInputState
{
    /// <summary>Creates a text-input state.</summary>
    public TextInputState(string variableName, string prompt, string initialValue = "")
    {
        ContractCollections.RequireNonEmpty(variableName, nameof(variableName));
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(initialValue);

        VariableName = variableName;
        Prompt = prompt;
        InitialValue = initialValue;
    }

    /// <summary>Gets the variable that receives the submitted text.</summary>
    public string VariableName { get; }

    public string Prompt { get; }

    public string InitialValue { get; }
}
