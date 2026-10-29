using Velora.Rendering;

namespace Velora.Text.Tests;

public sealed class RichTextParserTests
{
    [Fact]
    public void ParsesStylesAndInlineCommandsIntoPlatformNeutralRuneRanges()
    {
        RichTextDocument document = RichTextParser.Parse(
            "A{bold}🙂B{/bold}{color:#123456}C{/color}{wait:0.5}{voice:line.ogg}");

        Assert.Equal("A🙂BC", document.PlainText);
        Assert.Equal(4, document.RuneCount);
        Assert.Collection(
            document.Runs,
            run =>
            {
                Assert.Equal((0, 1, "A", false), (run.StartRune, run.RuneLength, run.Text, run.Style.Bold));
                Assert.Equal(RichTextStyle.Default.Color, run.Style.Color);
            },
            run => Assert.Equal((1, 2, "🙂B", true), (run.StartRune, run.RuneLength, run.Text, run.Style.Bold)),
            run =>
            {
                Assert.Equal((3, 1, "C"), (run.StartRune, run.RuneLength, run.Text));
                Assert.Equal(new ColorRgba(0x12, 0x34, 0x56), run.Style.Color);
            });
        Assert.Collection(
            document.Commands,
            command => Assert.Equal((4, "wait", "0.5"), (command.RuneIndex, command.Command, command.Argument)),
            command => Assert.Equal((4, "voice", "line.ogg"), (command.RuneIndex, command.Command, command.Argument)));
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void InvalidColorIsRecoverableAndUnclosedBraceRemainsLiteral()
    {
        RichTextDocument document = RichTextParser.Parse("A{color:nope}B{unterminated");

        Assert.Equal("AB{unterminated", document.PlainText);
        Assert.Equal(RichTextStyle.Default.Color, document.Runs[^1].Style.Color);
        Assert.Equal("TXT001", Assert.Single(document.Diagnostics).Id);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void VisibleTextSlicesByUnicodeScalarNotUtf16CodeUnit()
    {
        RichTextDocument document = RichTextParser.Parse("A🙂Б");

        Assert.Equal("A", document.GetVisibleText(1));
        Assert.Equal("A🙂", document.GetVisibleText(2));
        Assert.Equal("A🙂Б", document.GetVisibleText(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetVisibleText(4));
    }

    [Fact]
    public void UnknownClosedTagBecomesInlineCommandAtCurrentRune()
    {
        RichTextDocument document = RichTextParser.Parse("Hi{shake_screen:0.3}!");

        InlineTextCommand command = Assert.Single(document.Commands);
        Assert.Equal(2, command.RuneIndex);
        Assert.Equal("shake_screen", command.Command);
        Assert.Equal("Hi!", document.PlainText);
    }
}
