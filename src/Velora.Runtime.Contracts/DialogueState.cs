namespace Velora.Runtime.Contracts;

/// <summary>Captures the currently presented dialogue independently of a text renderer.</summary>
public sealed record DialogueState
{
    /// <summary>Creates a dialogue state.</summary>
    public DialogueState(
        string speakerId,
        string speakerName,
        string speakerColor,
        string text,
        int revealedCodePointCount,
        bool isComplete)
    {
        ArgumentNullException.ThrowIfNull(speakerId);
        ArgumentNullException.ThrowIfNull(speakerName);
        ArgumentNullException.ThrowIfNull(speakerColor);
        ArgumentNullException.ThrowIfNull(text);

        int codePointCount = text.EnumerateRunes().Count();
        if (revealedCodePointCount < 0 || revealedCodePointCount > codePointCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revealedCodePointCount),
                revealedCodePointCount,
                $"The reveal count must range from 0 through {codePointCount} Unicode code points.");
        }

        if (isComplete && revealedCodePointCount != codePointCount)
        {
            throw new ArgumentException(
                "Complete dialogue must reveal every Unicode code point.",
                nameof(isComplete));
        }

        SpeakerId = speakerId;
        SpeakerName = speakerName;
        SpeakerColor = speakerColor;
        Text = text;
        RevealedCodePointCount = revealedCodePointCount;
        IsComplete = isComplete;
    }

    /// <summary>Gets the VelScript character identifier, or an empty string for narration.</summary>
    public string SpeakerId { get; }

    /// <summary>Gets the localized display name, or an empty string for narration.</summary>
    public string SpeakerName { get; }

    /// <summary>Gets the presentation-neutral color text declared for the speaker.</summary>
    public string SpeakerColor { get; }

    /// <summary>Gets the localized and interpolated dialogue text.</summary>
    public string Text { get; }

    /// <summary>Gets the revealed length measured in Unicode code points.</summary>
    public int RevealedCodePointCount { get; }

    /// <summary>Gets whether typewriter reveal is complete.</summary>
    public bool IsComplete { get; }
}

/// <summary>An immutable dialogue-history entry used by backlog consumers.</summary>
public sealed record DialogueHistoryEntry
{
    /// <summary>Creates a dialogue-history entry.</summary>
    public DialogueHistoryEntry(string speakerId, string speakerName, string speakerColor, string text)
    {
        ArgumentNullException.ThrowIfNull(speakerId);
        ArgumentNullException.ThrowIfNull(speakerName);
        ArgumentNullException.ThrowIfNull(speakerColor);
        ArgumentNullException.ThrowIfNull(text);

        SpeakerId = speakerId;
        SpeakerName = speakerName;
        SpeakerColor = speakerColor;
        Text = text;
    }

    public string SpeakerId { get; }

    public string SpeakerName { get; }

    public string SpeakerColor { get; }

    public string Text { get; }
}
