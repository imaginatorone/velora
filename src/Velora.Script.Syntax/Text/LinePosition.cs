namespace Velora.Script.Syntax.Text;

/// <summary>
/// A zero-based line and UTF-16 character position.
/// </summary>
public readonly record struct LinePosition : IComparable<LinePosition>
{
    public LinePosition(int line, int character)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(character);

        Line = line;
        Character = character;
    }

    public int Line { get; }

    public int Character { get; }

    public int CompareTo(LinePosition other)
    {
        int lineComparison = Line.CompareTo(other.Line);
        return lineComparison != 0 ? lineComparison : Character.CompareTo(other.Character);
    }

    public static bool operator <(LinePosition left, LinePosition right) => left.CompareTo(right) < 0;

    public static bool operator <=(LinePosition left, LinePosition right) => left.CompareTo(right) <= 0;

    public static bool operator >(LinePosition left, LinePosition right) => left.CompareTo(right) > 0;

    public static bool operator >=(LinePosition left, LinePosition right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"({Line},{Character})";
}
