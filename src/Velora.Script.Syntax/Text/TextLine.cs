namespace Velora.Script.Syntax.Text;

/// <summary>
/// Describes one source line and its optional line break.
/// </summary>
public readonly record struct TextLine
{
    public TextLine(int lineNumber, int start, int length, int lineBreakLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(lineBreakLength);
        _ = checked(start + length + lineBreakLength);

        LineNumber = lineNumber;
        Start = start;
        Length = length;
        LineBreakLength = lineBreakLength;
    }

    public int LineNumber { get; }

    public int Start { get; }

    public int Length { get; }

    public int LineBreakLength { get; }

    public int End => Start + Length;

    public int EndIncludingLineBreak => End + LineBreakLength;

    public TextSpan Span => new(Start, Length);

    public TextSpan SpanIncludingLineBreak => new(Start, Length + LineBreakLength);
}
