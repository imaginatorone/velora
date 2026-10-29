namespace Velora.Runtime.Contracts;

/// <summary>Captures one runtime-owned numeric tween without renderer resources.</summary>
public sealed record TweenState
{
    /// <summary>Creates an active tween state.</summary>
    public TweenState(
        ulong id,
        string targetId,
        string property,
        double from,
        double to,
        double current,
        TimeSpan duration,
        TimeSpan elapsed,
        string easing,
        double? returnTo = null)
    {
        ContractCollections.RequireNonEmpty(targetId, nameof(targetId));
        ContractCollections.RequireNonEmpty(property, nameof(property));
        ContractCollections.RequireFinite(from, nameof(from));
        ContractCollections.RequireFinite(to, nameof(to));
        ContractCollections.RequireFinite(current, nameof(current));
        ContractCollections.RequireNonNegative(duration, nameof(duration));
        ContractCollections.RequireNonNegative(elapsed, nameof(elapsed));
        ContractCollections.RequireNonEmpty(easing, nameof(easing));

        if (duration == TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A tween duration must be positive.");
        }

        if (elapsed > duration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                elapsed,
                "The elapsed duration must not exceed the tween duration.");
        }

        Id = id;
        TargetId = targetId;
        Property = property;
        From = from;
        To = to;
        Current = current;
        Duration = duration;
        Elapsed = elapsed;
        Easing = easing;
        if (returnTo is { } endpoint)
        {
            ContractCollections.RequireFinite(endpoint, nameof(returnTo));
        }

        ReturnTo = returnTo;
    }

    public ulong Id { get; }

    public string TargetId { get; }

    public string Property { get; }

    public double From { get; }

    public double To { get; }

    public double Current { get; }

    public TimeSpan Duration { get; }

    public TimeSpan Elapsed { get; }

    public string Easing { get; }

    /// <summary>Optional second leg used by legacy emotion animations.</summary>
    public double? ReturnTo { get; }

    public bool IsComplete => Elapsed == Duration;
}
