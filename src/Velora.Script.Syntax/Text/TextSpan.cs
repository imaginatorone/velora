namespace Velora.Script.Syntax.Text;

/// <summary>
/// Describes a half-open range of UTF-16 code units in a <see cref="SourceText"/>.
/// </summary>
public readonly record struct TextSpan
{
    public TextSpan(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Start = start;
        Length = length;
        _ = End;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => checked(Start + Length);

    public bool IsEmpty => Length == 0;

    public static TextSpan FromBounds(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        return new TextSpan(start, checked(end - start));
    }

    public bool Contains(int position) => position >= Start && position < End;

    public bool Contains(TextSpan span) => span.Start >= Start && span.End <= End;

    public bool OverlapsWith(TextSpan span) => Start < span.End && span.Start < End;

    public override string ToString() => $"[{Start}..{End})";
}
