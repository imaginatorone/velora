namespace Velora.Script.Syntax.Text;

/// <summary>
/// A half-open range expressed as zero-based line positions.
/// </summary>
public readonly record struct LinePositionSpan
{
    public LinePositionSpan(LinePosition start, LinePosition end)
    {
        if (end.CompareTo(start) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The end position cannot precede the start position.");
        }

        Start = start;
        End = end;
    }

    public LinePosition Start { get; }

    public LinePosition End { get; }

    public override string ToString() => $"{Start}-{End}";
}
