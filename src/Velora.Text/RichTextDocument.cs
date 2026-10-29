using System.Collections.ObjectModel;
using System.Text;
using Velora.Rendering;

namespace Velora.Text;

public readonly record struct RichTextStyle(ColorRgba Color, bool Bold = false, bool Italic = false)
{
    public static RichTextStyle Default { get; } = new(new ColorRgba(255, 255, 255));
}

public sealed record RichTextRun(
    int StartRune,
    int RuneLength,
    string Text,
    RichTextStyle Style);

public sealed record InlineTextCommand(
    int RuneIndex,
    string Command,
    string Argument,
    int SourceOffset);

public enum RichTextDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record RichTextDiagnostic(
    string Id,
    RichTextDiagnosticSeverity Severity,
    string Message,
    int SourceOffset,
    int SourceLength);

/// <summary>Presentation-neutral parsed dialogue text and source-ordered commands.</summary>
public sealed class RichTextDocument
{
    private readonly ReadOnlyCollection<RichTextRun> _runs;
    private readonly ReadOnlyCollection<InlineTextCommand> _commands;
    private readonly ReadOnlyCollection<RichTextDiagnostic> _diagnostics;
    private readonly int[] _runeEndOffsets;

    internal RichTextDocument(
        string rawText,
        string plainText,
        RichTextRun[] runs,
        InlineTextCommand[] commands,
        RichTextDiagnostic[] diagnostics)
    {
        RawText = rawText;
        PlainText = plainText;
        _runs = Array.AsReadOnly(runs);
        _commands = Array.AsReadOnly(commands);
        _diagnostics = Array.AsReadOnly(diagnostics);
        _runeEndOffsets = BuildRuneEndOffsets(plainText);
    }

    public string RawText { get; }

    public string PlainText { get; }

    public int RuneCount => _runeEndOffsets.Length;

    public IReadOnlyList<RichTextRun> Runs => _runs;

    public IReadOnlyList<InlineTextCommand> Commands => _commands;

    public IReadOnlyList<RichTextDiagnostic> Diagnostics => _diagnostics;

    public string GetVisibleText(int runeCount)
    {
        if (runeCount < 0 || runeCount > RuneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(runeCount));
        }

        if (runeCount == 0)
        {
            return string.Empty;
        }

        return PlainText[.._runeEndOffsets[runeCount - 1]];
    }

    private static int[] BuildRuneEndOffsets(string text)
    {
        List<int> offsets = [];
        int offset = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            offset += rune.Utf16SequenceLength;
            offsets.Add(offset);
        }

        return offsets.ToArray();
    }
}
