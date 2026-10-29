namespace Velora.Audio;

public sealed record AudioTrackSnapshot(
    string Id,
    string AssetPath,
    AudioChannel Channel,
    double Volume,
    double TargetVolume,
    double FadePerSecond,
    bool Playing,
    bool Looping);

public readonly record struct AudioMixSample(
    string TrackId,
    AudioChannel Channel,
    double EffectiveVolume,
    bool Looping);

internal sealed class AudioTrackState
{
    public required string Id { get; set; }

    public required string AssetPath { get; init; }

    public required AudioChannel Channel { get; init; }

    public required bool Looping { get; init; }

    public double Volume { get; set; }

    public double TargetVolume { get; set; }

    public double FadePerSecond { get; set; }

    public bool Playing { get; set; }

    public AudioTrackSnapshot Snapshot() => new(
        Id,
        AssetPath,
        Channel,
        Volume,
        TargetVolume,
        FadePerSecond,
        Playing,
        Looping);

    public static AudioTrackState Restore(AudioTrackSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AssetPath);
        if (!Enum.IsDefined(snapshot.Channel))
        {
            throw new ArgumentException("The snapshot contains an unknown audio channel.", nameof(snapshot));
        }

        ValidateFinite(snapshot.Volume, nameof(snapshot));
        ValidateFinite(snapshot.TargetVolume, nameof(snapshot));
        ValidateFinite(snapshot.FadePerSecond, nameof(snapshot));
        return new AudioTrackState
        {
            Id = snapshot.Id,
            AssetPath = snapshot.AssetPath,
            Channel = snapshot.Channel,
            Volume = snapshot.Volume,
            TargetVolume = snapshot.TargetVolume,
            FadePerSecond = snapshot.FadePerSecond,
            Playing = snapshot.Playing,
            Looping = snapshot.Looping,
        };
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("Audio track state must contain finite numbers.", parameterName);
        }
    }
}
