namespace Velora.Script.Compiler;

/// <summary>Identifies a half-open UTF-16 source range without retaining syntax objects.</summary>
public readonly record struct SourceLocation
{
    public SourceLocation(string filePath, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        FilePath = filePath;
        Start = start;
        Length = length;
        _ = End;
    }

    public string FilePath { get; }

    public int Start { get; }

    public int Length { get; }

    public int End => checked(Start + Length);
}
