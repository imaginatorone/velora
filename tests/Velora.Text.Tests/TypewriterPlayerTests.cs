namespace Velora.Text.Tests;

public sealed class TypewriterPlayerTests
{
    [Fact]
    public void RevealsUnicodeScalarsAtCallerDrivenSpeed()
    {
        var player = new TypewriterPlayer(charactersPerSecond: 2);
        player.Start(RichTextParser.Parse("A🙂B"));

        _ = player.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal("A", player.VisibleText);
        _ = player.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal("A🙂", player.VisibleText);
        _ = player.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal("A🙂B", player.VisibleText);
        Assert.True(player.IsComplete);
    }

    [Fact]
    public void WaitSpeedAndExternalCommandsFireInStableSourceOrder()
    {
        var player = new TypewriterPlayer(charactersPerSecond: 10);
        player.Start(RichTextParser.Parse("A{wait:0.5}{speed:2}{voice:line.ogg}B"));

        IReadOnlyList<InlineCommandFired> first = player.Update(TimeSpan.FromSeconds(0.1));
        Assert.Single(first);
        Assert.Equal("voice", first[0].Command);
        Assert.Equal(0.5, player.WaitRemainingSeconds);
        Assert.Equal(2, player.CharactersPerSecond);
        Assert.Equal("A", player.VisibleText);

        Assert.Empty(player.Update(TimeSpan.FromSeconds(0.25)));
        Assert.Equal(0.25, player.WaitRemainingSeconds, precision: 8);
        Assert.Empty(player.Update(TimeSpan.FromSeconds(0.25)));
        Assert.Equal(0, player.WaitRemainingSeconds);
        _ = player.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal("AB", player.VisibleText);
    }

    [Fact]
    public void SkipFiresPendingExternalActionsButNotTimingControls()
    {
        var player = new TypewriterPlayer();
        player.Start(RichTextParser.Parse("A{wait:1}{play_sfx:bell.ogg}{speed:60}{voice:line.ogg}B"));

        IReadOnlyList<InlineCommandFired> fired = player.Skip();

        Assert.Equal(["play_sfx", "voice"], fired.Select(static command => command.Command));
        Assert.Equal("AB", player.VisibleText);
        Assert.True(player.IsComplete);
    }

    [Fact]
    public void CaptureAndRestoreContinueDeterministically()
    {
        RichTextDocument document = RichTextParser.Parse("A{speed:4}BCD");
        var original = new TypewriterPlayer(charactersPerSecond: 2);
        original.Start(document);
        _ = original.Update(TimeSpan.FromSeconds(0.5));
        TypewriterSnapshot snapshot = original.CaptureState();

        var restored = new TypewriterPlayer();
        restored.RestoreState(document, snapshot);
        _ = original.Update(TimeSpan.FromSeconds(0.5));
        _ = restored.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(original.VisibleText, restored.VisibleText);
        TypewriterSnapshot originalState = original.CaptureState();
        TypewriterSnapshot restoredState = restored.CaptureState();
        Assert.Equal(originalState.VisibleRunes, restoredState.VisibleRunes);
        Assert.Equal(originalState.CharacterAccumulator, restoredState.CharacterAccumulator);
        Assert.Equal(originalState.WaitRemainingSeconds, restoredState.WaitRemainingSeconds);
        Assert.Equal(originalState.CharactersPerSecond, restoredState.CharactersPerSecond);
        Assert.Equal(originalState.FiredCommandIndexes, restoredState.FiredCommandIndexes);
        Assert.Equal(originalState.IsComplete, restoredState.IsComplete);
    }

    [Fact]
    public void RejectsInvalidDeltaAndCorruptSnapshots()
    {
        RichTextDocument document = RichTextParser.Parse("A");
        var player = new TypewriterPlayer();
        player.Start(document);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Update(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentException>(() => player.RestoreState(
            document,
            new TypewriterSnapshot(2, 0, 0, 30, [], false)));
        Assert.Throws<ArgumentException>(() => player.RestoreState(
            document,
            new TypewriterSnapshot(0, 0, 0, 30, [0], false)));
    }
}
