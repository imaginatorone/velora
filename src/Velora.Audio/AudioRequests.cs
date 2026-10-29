namespace Velora.Audio;

public sealed record AudioPlayRequest
{
    public AudioPlayRequest(
        string trackId,
        string assetPath,
        AudioChannel channel,
        double fadeInSeconds = 0,
        double volume = 1,
        double crossfadeSeconds = 0,
        bool? loop = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown audio channel.");
        }

        ValidateDuration(fadeInSeconds, nameof(fadeInSeconds));
        ValidateDuration(crossfadeSeconds, nameof(crossfadeSeconds));
        if (!double.IsFinite(volume))
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Track volume must be finite.");
        }

        TrackId = trackId;
        AssetPath = assetPath;
        Channel = channel;
        FadeInSeconds = fadeInSeconds;
        Volume = volume;
        CrossfadeSeconds = crossfadeSeconds;
        Loop = loop ?? channel == AudioChannel.Bgm;
    }

    public string TrackId { get; }

    public string AssetPath { get; }

    public AudioChannel Channel { get; }

    public double FadeInSeconds { get; }

    public double Volume { get; }

    public double CrossfadeSeconds { get; }

    public bool Loop { get; }

    private static void ValidateDuration(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Audio durations must be finite and non-negative.");
        }
    }
}

public sealed record AudioStopRequest
{
    public AudioStopRequest(string trackId, double fadeOutSeconds = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        if (!double.IsFinite(fadeOutSeconds) || fadeOutSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fadeOutSeconds),
                fadeOutSeconds,
                "Fade-out duration must be finite and non-negative.");
        }

        TrackId = trackId;
        FadeOutSeconds = fadeOutSeconds;
    }

    public string TrackId { get; }

    public double FadeOutSeconds { get; }
}
