using System.Globalization;

namespace Velora.Runtime.Contracts;

/// <summary>
/// Identifies an instruction by its zero-based ordinal within one compiled program.
/// The value is deliberately not stable across recompilation.
/// </summary>
public readonly record struct InstructionId : IComparable<InstructionId>
{
    private readonly long _encodedOrdinal;

    private InstructionId(int ordinal, bool _)
    {
        _encodedOrdinal = (long)ordinal + 1;
    }

    /// <summary>Gets the zero-based ordinal, or <c>-1</c> for an invalid/default value.</summary>
    public int Ordinal => (int)(_encodedOrdinal - 1);

    /// <summary>Gets whether this value identifies an instruction.</summary>
    public bool IsValid => _encodedOrdinal > 0;

    /// <summary>Creates an instruction identifier from a zero-based ordinal.</summary>
    public static InstructionId FromOrdinal(int ordinal)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                ordinal,
                "The instruction ordinal must not be negative.");
        }

        return new InstructionId(ordinal, true);
    }

    /// <inheritdoc />
    public int CompareTo(InstructionId other) => _encodedOrdinal.CompareTo(other._encodedOrdinal);

    public static bool operator <(InstructionId left, InstructionId right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(InstructionId left, InstructionId right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(InstructionId left, InstructionId right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(InstructionId left, InstructionId right) =>
        left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() =>
        IsValid ? Ordinal.ToString(CultureInfo.InvariantCulture) : "<invalid>";
}
