using System.Collections.ObjectModel;
using System.Text;

namespace Velora.Script.Syntax.Text;

/// <summary>
/// Immutable UTF-16 source text with a precomputed line map.
/// </summary>
public sealed class SourceText
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];

    private readonly string _text;
    private readonly ReadOnlyCollection<TextLine> _lines;

    private SourceText(string text, string filePath)
    {
        _text = text;
        FilePath = filePath;
        _lines = Array.AsReadOnly(ParseLines(text));
    }

    public string FilePath { get; }

    public int Length => _text.Length;

    public IReadOnlyList<TextLine> Lines => _lines;

    public char this[int position] => _text[position];

    public static SourceText From(string text, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);
        return new SourceText(text, filePath);
    }

    /// <summary>
    /// Reads UTF-8 text. A leading UTF-8 byte-order mark is accepted and omitted from the in-memory text.
    /// Invalid byte sequences are represented by U+FFFD.
    /// </summary>
    public static SourceText Read(Stream stream, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(filePath);

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        ReadOnlySpan<byte> bytes = buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length));

        if (bytes.StartsWith(Utf8Preamble))
        {
            bytes = bytes[Utf8Preamble.Length..];
        }

        return new SourceText(Utf8.GetString(bytes), filePath);
    }

    public static SourceText ReadFrom(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using FileStream stream = File.OpenRead(filePath);
        return Read(stream, filePath);
    }

    public string GetText(TextSpan span)
    {
        ValidateSpan(span);
        return _text.Substring(span.Start, span.Length);
    }

    public TextLine GetLineFromPosition(int position)
    {
        return _lines[GetLineIndex(position)];
    }

    public int GetLineIndex(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Length);

        int lower = 0;
        int upper = _lines.Count - 1;

        while (lower <= upper)
        {
            int index = lower + ((upper - lower) / 2);
            TextLine line = _lines[index];

            if (position < line.Start)
            {
                upper = index - 1;
            }
            else if (index + 1 < _lines.Count && position >= _lines[index + 1].Start)
            {
                lower = index + 1;
            }
            else
            {
                return index;
            }
        }

        throw new InvalidOperationException("The line map does not cover the requested source position.");
    }

    public LinePosition GetLinePosition(int position)
    {
        TextLine line = GetLineFromPosition(position);
        return new LinePosition(line.LineNumber, position - line.Start);
    }

    public LinePositionSpan GetLinePositionSpan(TextSpan span)
    {
        ValidateSpan(span);
        return new LinePositionSpan(GetLinePosition(span.Start), GetLinePosition(span.End));
    }

    public override string ToString() => _text;

    private static TextLine[] ParseLines(string text)
    {
        List<TextLine> lines = [];
        int lineNumber = 0;
        int lineStart = 0;
        int position = 0;

        while (position < text.Length)
        {
            int lineBreakLength = GetLineBreakLength(text, position);
            if (lineBreakLength == 0)
            {
                position++;
                continue;
            }

            lines.Add(new TextLine(lineNumber, lineStart, position - lineStart, lineBreakLength));
            lineNumber++;
            position += lineBreakLength;
            lineStart = position;
        }

        lines.Add(new TextLine(lineNumber, lineStart, text.Length - lineStart, lineBreakLength: 0));
        return [.. lines];
    }

    private static int GetLineBreakLength(string text, int position)
    {
        char current = text[position];
        if (current == '\r' && position + 1 < text.Length && text[position + 1] == '\n')
        {
            return 2;
        }

        return current is '\r' or '\n' ? 1 : 0;
    }

    private void ValidateSpan(TextSpan span)
    {
        if (span.End > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "The span extends beyond the source text.");
        }
    }
}
