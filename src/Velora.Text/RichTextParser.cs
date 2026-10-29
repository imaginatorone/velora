using System.Text;
using Velora.Rendering;

namespace Velora.Text;

/// <summary>Parses Velora style and inline-action tags without presentation APIs.</summary>
public static class RichTextParser
{
    public static RichTextDocument Parse(string rawText)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        StringBuilder plain = new(rawText.Length);
        StringBuilder currentRun = new(rawText.Length);
        List<RichTextRun> runs = [];
        List<InlineTextCommand> commands = [];
        List<RichTextDiagnostic> diagnostics = [];
        RichTextStyle style = RichTextStyle.Default;
        int runStart = 0;
        int runeCount = 0;
        int sourceOffset = 0;

        while (sourceOffset < rawText.Length)
        {
            if (rawText[sourceOffset] == '{')
            {
                int close = rawText.IndexOf('}', sourceOffset + 1);
                if (close >= 0)
                {
                    string content = rawText[(sourceOffset + 1)..close];
                    int colon = content.IndexOf(':');
                    string command = colon >= 0 ? content[..colon] : content;
                    string argument = colon >= 0 ? content[(colon + 1)..] : string.Empty;

                    if (command is "color" or "/color" or "bold" or "/bold")
                    {
                        FlushRun(currentRun, runs, ref runStart, runeCount, style);
                        style = ApplyStyle(command, argument, style, diagnostics, sourceOffset, close - sourceOffset + 1);
                    }
                    else
                    {
                        commands.Add(new InlineTextCommand(runeCount, command, argument, sourceOffset));
                    }

                    sourceOffset = close + 1;
                    continue;
                }
            }

            if (!Rune.TryGetRuneAt(rawText, sourceOffset, out Rune rune))
            {
                rune = Rune.ReplacementChar;
            }

            string value = rune.ToString();
            plain.Append(value);
            currentRun.Append(value);
            runeCount++;
            sourceOffset += rune.Utf16SequenceLength;
        }

        FlushRun(currentRun, runs, ref runStart, runeCount, style);
        return new RichTextDocument(
            rawText,
            plain.ToString(),
            runs.ToArray(),
            commands.ToArray(),
            diagnostics.ToArray());
    }

    private static RichTextStyle ApplyStyle(
        string command,
        string argument,
        RichTextStyle current,
        List<RichTextDiagnostic> diagnostics,
        int sourceOffset,
        int sourceLength)
    {
        switch (command)
        {
            case "bold":
                return current with { Bold = true };
            case "/bold":
                return current with { Bold = false };
            case "/color":
                return current with { Color = RichTextStyle.Default.Color };
            case "color" when ColorRgba.TryParseVelScript(argument, out ColorRgba color):
                return current with { Color = color };
            case "color":
                diagnostics.Add(new RichTextDiagnostic(
                    "TXT001",
                    RichTextDiagnosticSeverity.Warning,
                    $"Invalid rich-text color '{argument}'; the current color is retained.",
                    sourceOffset,
                    sourceLength));
                return current;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private static void FlushRun(
        StringBuilder current,
        List<RichTextRun> runs,
        ref int runStart,
        int runeCount,
        RichTextStyle style)
    {
        if (current.Length == 0)
        {
            runStart = runeCount;
            return;
        }

        runs.Add(new RichTextRun(runStart, runeCount - runStart, current.ToString(), style));
        current.Clear();
        runStart = runeCount;
    }
}
