using System.Text;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Tests.Text;

public sealed class SourceTextTests
{
    [Fact]
    public void LineMapUsesUtf16OffsetsAndRecognizesLfAndCrLf()
    {
        SourceText text = SourceText.From("α😀\r\nx\n");

        Assert.Equal(7, text.Length);
        Assert.Equal(3, text.Lines.Count);
        Assert.Equal(new TextLine(0, 0, 3, 2), text.Lines[0]);
        Assert.Equal(new TextLine(1, 5, 1, 1), text.Lines[1]);
        Assert.Equal(new TextLine(2, 7, 0, 0), text.Lines[2]);
        Assert.Equal(new LinePosition(0, 3), text.GetLinePosition(3));
        Assert.Equal(new LinePosition(1, 0), text.GetLinePosition(5));
        Assert.Equal(new LinePosition(2, 0), text.GetLinePosition(text.Length));
        Assert.Equal(
            new LinePositionSpan(new LinePosition(0, 1), new LinePosition(1, 1)),
            text.GetLinePositionSpan(TextSpan.FromBounds(1, 6)));
    }

    [Fact]
    public void Utf8ReaderToleratesAndRemovesBom()
    {
        byte[] payload = Encoding.UTF8.GetBytes("\uFEFFlabel start:\n");
        using MemoryStream stream = new(payload);

        SourceText text = SourceText.Read(stream, "story.vel");

        Assert.Equal("label start:\n", text.ToString());
        Assert.Equal("story.vel", text.FilePath);
    }

    [Fact]
    public void Utf8ReaderReplacesMalformedBytes()
    {
        using MemoryStream stream = new([0x61, 0xFF, 0x62]);

        SourceText text = SourceText.Read(stream);

        Assert.Equal("a\uFFFDb", text.ToString());
    }

    [Fact]
    public void EmptyTextStillHasOneLine()
    {
        SourceText text = SourceText.From(string.Empty);

        TextLine line = Assert.Single(text.Lines);
        Assert.Equal(new TextLine(0, 0, 0, 0), line);
        Assert.Equal(new LinePosition(0, 0), text.GetLinePosition(0));
    }
}
