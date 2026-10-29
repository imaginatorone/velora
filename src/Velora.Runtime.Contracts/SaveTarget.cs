namespace Velora.Runtime.Contracts;

/// <summary>Identifies a save by meaning instead of a magic numeric slot.</summary>
public readonly record struct SaveTarget
{
    private readonly int _encodedManualSlot;

    public const int MinimumManualSlot = 0;
    public const int MaximumManualSlot = 20;

    private SaveTarget(SaveTargetKind kind, int manualSlot)
    {
        Kind = kind;
        _encodedManualSlot = kind == SaveTargetKind.Manual ? checked(manualSlot + 1) : 0;
    }

    public SaveTargetKind Kind { get; }

    /// <summary>Gets the legacy slot for a manual target; otherwise <c>-1</c>.</summary>
    public int ManualSlot => Kind == SaveTargetKind.Manual ? _encodedManualSlot - 1 : -1;

    public bool IsValid => Kind is SaveTargetKind.Manual or SaveTargetKind.Quick or SaveTargetKind.Auto;

    public static SaveTarget Manual(int slot)
    {
        if (slot is < MinimumManualSlot or > MaximumManualSlot)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                slot,
                $"Manual save slots range from {MinimumManualSlot} through {MaximumManualSlot}.");
        }

        return new SaveTarget(SaveTargetKind.Manual, slot);
    }

    public static SaveTarget Quick { get; } = new(SaveTargetKind.Quick, -1);

    public static SaveTarget Auto { get; } = new(SaveTargetKind.Auto, -1);
}

public enum SaveTargetKind
{
    Unknown = 0,
    Manual,
    Quick,
    Auto,
}
