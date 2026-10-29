namespace Velora.Runtime.Contracts;

/// <summary>Captures a deterministic timed wait in progress.</summary>
public sealed record WaitState
{
    /// <summary>Creates a timed wait.</summary>
    public WaitState(TimeSpan duration, TimeSpan remaining)
    {
        ContractCollections.RequireNonNegative(duration, nameof(duration));
        ContractCollections.RequireNonNegative(remaining, nameof(remaining));
        if (remaining > duration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remaining),
                remaining,
                "The remaining duration must not exceed the original duration.");
        }

        Duration = duration;
        Remaining = remaining;
    }

    public TimeSpan Duration { get; }

    public TimeSpan Remaining { get; }
}
