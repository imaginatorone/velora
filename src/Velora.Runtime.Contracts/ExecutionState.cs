namespace Velora.Runtime.Contracts;

/// <summary>Describes the runtime execution phase.</summary>
public enum RuntimeExecutionStatus
{
    Unknown = 0,
    NotStarted,
    Running,
    WaitingForAdvance,
    WaitingForChoice,
    WaitingForTextInput,
    WaitingForTime,
    Finished,
}

/// <summary>
/// Captures narrative control flow and the single active suspension reason.
/// </summary>
public sealed record ExecutionState
{
    /// <summary>Creates an execution state.</summary>
    public ExecutionState(
        RuntimeExecutionStatus status,
        InstructionId currentInstruction,
        string currentLabel,
        WaitState? wait = null,
        ChoiceState? choice = null,
        TextInputState? textInput = null)
    {
        if (status is RuntimeExecutionStatus.Unknown || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The execution status is invalid.");
        }

        ArgumentNullException.ThrowIfNull(currentLabel);
        if (status is not RuntimeExecutionStatus.NotStarted &&
            status is not RuntimeExecutionStatus.Finished &&
            !currentInstruction.IsValid)
        {
            throw new ArgumentException(
                "An active execution state requires a valid current instruction.",
                nameof(currentInstruction));
        }

        ValidatePendingState(status, wait, choice, textInput);

        Status = status;
        CurrentInstruction = currentInstruction;
        CurrentLabel = currentLabel;
        Wait = wait;
        Choice = choice;
        TextInput = textInput;
    }

    public RuntimeExecutionStatus Status { get; }

    public InstructionId CurrentInstruction { get; }

    public string CurrentLabel { get; }

    public WaitState? Wait { get; }

    public ChoiceState? Choice { get; }

    public TextInputState? TextInput { get; }

    private static void ValidatePendingState(
        RuntimeExecutionStatus status,
        WaitState? wait,
        ChoiceState? choice,
        TextInputState? textInput)
    {
        bool valid = status switch
        {
            RuntimeExecutionStatus.WaitingForTime => wait is not null && choice is null && textInput is null,
            RuntimeExecutionStatus.WaitingForChoice => wait is null && choice is not null && textInput is null,
            RuntimeExecutionStatus.WaitingForTextInput => wait is null && choice is null && textInput is not null,
            _ => wait is null && choice is null && textInput is null,
        };

        if (!valid)
        {
            throw new ArgumentException(
                "Exactly the pending state corresponding to the execution status must be supplied.");
        }
    }
}
