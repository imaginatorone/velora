using System.Collections.ObjectModel;

namespace Velora.Audio;

public sealed record AudioMixerSnapshot(
    double MasterVolume,
    double BgmVolume,
    double SfxVolume,
    double VoiceVolume,
    bool DuckEnabled,
    double DuckLevel,
    double CurrentDuck,
    ulong TrackSequence,
    IReadOnlyList<AudioTrackSnapshot> Tracks);

/// <summary>
/// Deterministic audio control state shared by device backends. It owns fades,
/// crossfades, channel gains, and voice ducking, but no native audio resources.
/// </summary>
public sealed class AudioMixerState
{
    private readonly Dictionary<string, AudioTrackState> _tracks = new(StringComparer.Ordinal);
    private ulong _trackSequence;

    public double MasterVolume { get; private set; } = 1;

    public double BgmVolume { get; private set; } = 0.7;

    public double SfxVolume { get; private set; } = 1;

    public double VoiceVolume { get; private set; } = 1;

    public bool DuckEnabled { get; private set; } = true;

    public double DuckLevel { get; private set; } = 0.3;

    public double CurrentDuck { get; private set; } = 1;

    public IReadOnlyList<AudioTrackSnapshot> Tracks =>
        new ReadOnlyCollection<AudioTrackSnapshot>(
        [
            .. _tracks.Values
                .OrderBy(static track => track.Id, StringComparer.Ordinal)
                .Select(static track => track.Snapshot()),
        ]);

    public void Play(AudioPlayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tracks.Remove(request.TrackId, out AudioTrackState? oldTrack)
            && request.CrossfadeSeconds > 0)
        {
            oldTrack.TargetVolume = 0;
            oldTrack.FadePerSecond = -oldTrack.Volume / request.CrossfadeSeconds;
            string oldId = $"{request.TrackId}_old_{++_trackSequence}";
            oldTrack.Id = oldId;
            _tracks.Add(oldId, oldTrack);
        }

        double initialVolume = request.FadeInSeconds > 0 ? 0 : request.Volume;
        _tracks[request.TrackId] = new AudioTrackState
        {
            Id = request.TrackId,
            AssetPath = request.AssetPath,
            Channel = request.Channel,
            Looping = request.Loop,
            Volume = initialVolume,
            TargetVolume = request.Volume,
            FadePerSecond = request.FadeInSeconds > 0 ? request.Volume / request.FadeInSeconds : 0,
            Playing = true,
        };
    }

    public string PlayOneShot(string assetPath, double volume = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        string id = $"sfx_{++_trackSequence}";
        Play(new AudioPlayRequest(id, assetPath, AudioChannel.Sfx, volume: volume, loop: false));
        return id;
    }

    public void Stop(AudioStopRequest request)
    {
        if (!_tracks.TryGetValue(request.TrackId, out AudioTrackState? track))
        {
            return;
        }

        if (request.FadeOutSeconds <= 0)
        {
            _tracks.Remove(request.TrackId);
            return;
        }

        track.TargetVolume = 0;
        track.FadePerSecond = -track.Volume / request.FadeOutSeconds;
    }

    public void StopAll(double fadeOutSeconds = 0)
    {
        ValidateDuration(fadeOutSeconds, nameof(fadeOutSeconds));
        if (fadeOutSeconds <= 0)
        {
            _tracks.Clear();
            return;
        }

        foreach (AudioTrackState track in _tracks.Values)
        {
            track.TargetVolume = 0;
            track.FadePerSecond = -track.Volume / fadeOutSeconds;
        }
    }

    public bool Complete(string trackId)
    {
        ArgumentNullException.ThrowIfNull(trackId);
        return _tracks.Remove(trackId);
    }

    public IReadOnlyList<AudioMixSample> Update(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "Audio time cannot move backwards.");
        }

        double seconds = delta.TotalSeconds;
        bool primaryVoicePlaying = _tracks.TryGetValue("voice", out AudioTrackState? voice)
            && voice.Playing;
        double targetDuck = DuckEnabled && primaryVoicePlaying ? DuckLevel : 1;
        CurrentDuck += (targetDuck - CurrentDuck) * Math.Min(3 * seconds, 1);

        List<string> completedFades = [];
        foreach (AudioTrackState track in _tracks.Values)
        {
            if (track.FadePerSecond == 0)
            {
                continue;
            }

            track.Volume += track.FadePerSecond * seconds;
            if (track.FadePerSecond > 0 && track.Volume >= track.TargetVolume)
            {
                track.Volume = track.TargetVolume;
                track.FadePerSecond = 0;
            }
            else if (track.FadePerSecond < 0 && track.Volume <= track.TargetVolume)
            {
                track.Volume = track.TargetVolume;
                track.FadePerSecond = 0;
                if (track.Volume <= 0)
                {
                    completedFades.Add(track.Id);
                }
            }
        }

        foreach (string trackId in completedFades)
        {
            _tracks.Remove(trackId);
        }

        return
        [
            .. _tracks.Values
                .OrderBy(track => track.Id, StringComparer.Ordinal)
                .Select(track => new AudioMixSample(
                    track.Id,
                    track.Channel,
                    MasterVolume * GetChannelVolume(track.Channel) * track.Volume
                        * (track.Channel == AudioChannel.Bgm ? CurrentDuck : 1),
                    track.Looping)),
        ];
    }

    public void SetChannelVolumes(double master, double bgm, double sfx, double voice)
    {
        MasterVolume = ClampVolume(master, nameof(master));
        BgmVolume = ClampVolume(bgm, nameof(bgm));
        SfxVolume = ClampVolume(sfx, nameof(sfx));
        VoiceVolume = ClampVolume(voice, nameof(voice));
    }

    public void ConfigureDucking(bool enabled, double level)
    {
        DuckEnabled = enabled;
        DuckLevel = ClampVolume(level, nameof(level));
    }

    public AudioMixerSnapshot Capture() => new(
        MasterVolume,
        BgmVolume,
        SfxVolume,
        VoiceVolume,
        DuckEnabled,
        DuckLevel,
        CurrentDuck,
        _trackSequence,
        Tracks);

    public static AudioMixerState Restore(AudioMixerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Tracks);
        AudioMixerState mixer = new()
        {
            MasterVolume = ClampVolume(snapshot.MasterVolume, nameof(snapshot.MasterVolume)),
            BgmVolume = ClampVolume(snapshot.BgmVolume, nameof(snapshot.BgmVolume)),
            SfxVolume = ClampVolume(snapshot.SfxVolume, nameof(snapshot.SfxVolume)),
            VoiceVolume = ClampVolume(snapshot.VoiceVolume, nameof(snapshot.VoiceVolume)),
            DuckEnabled = snapshot.DuckEnabled,
            DuckLevel = ClampVolume(snapshot.DuckLevel, nameof(snapshot.DuckLevel)),
            CurrentDuck = ClampVolume(snapshot.CurrentDuck, nameof(snapshot.CurrentDuck)),
            _trackSequence = snapshot.TrackSequence,
        };

        foreach (AudioTrackSnapshot track in snapshot.Tracks)
        {
            if (!mixer._tracks.TryAdd(track.Id, AudioTrackState.Restore(track)))
            {
                throw new ArgumentException($"Duplicate audio track '{track.Id}' in snapshot.", nameof(snapshot));
            }
        }

        return mixer;
    }

    private double GetChannelVolume(AudioChannel channel) => channel switch
    {
        AudioChannel.Bgm => BgmVolume,
        AudioChannel.Sfx => SfxVolume,
        AudioChannel.Voice => VoiceVolume,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown audio channel."),
    };

    private static double ClampVolume(double volume, string parameterName)
    {
        if (!double.IsFinite(volume))
        {
            throw new ArgumentOutOfRangeException(parameterName, volume, "Volume must be finite.");
        }

        return Math.Clamp(volume, 0, 1);
    }

    private static void ValidateDuration(double duration, string parameterName)
    {
        if (!double.IsFinite(duration) || duration < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, duration, "Duration must be finite and non-negative.");
        }
    }
}
