using Velora.Audio;

namespace Velora.Audio.Tests;

public sealed class AudioMixerStateTests
{
    [Fact]
    public void BgmLoopsAndUsesLegacyDefaultChannelGain()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("bgm", "audio/theme.ogg", AudioChannel.Bgm));

        AudioMixSample sample = Assert.Single(mixer.Update(TimeSpan.Zero));

        Assert.True(sample.Looping);
        Assert.Equal(0.7, sample.EffectiveVolume, precision: 6);
    }

    [Fact]
    public void FadeInAdvancesDeterministicallyAndClampsAtTarget()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("voice", "audio/line.ogg", AudioChannel.Voice, fadeInSeconds: 2, volume: 0.8));

        Assert.Equal(0.2, Assert.Single(mixer.Update(TimeSpan.FromSeconds(0.5))).EffectiveVolume, precision: 6);
        Assert.Equal(0.8, Assert.Single(mixer.Update(TimeSpan.FromSeconds(10))).EffectiveVolume, precision: 6);
    }

    [Fact]
    public void CrossfadeKeepsOldTrackUntilItsFadeCompletes()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("bgm", "audio/old.ogg", AudioChannel.Bgm));
        mixer.Play(new AudioPlayRequest("bgm", "audio/new.ogg", AudioChannel.Bgm, crossfadeSeconds: 2));

        Assert.Equal(2, mixer.Tracks.Count);
        Assert.Contains(mixer.Tracks, track => track.Id.StartsWith("bgm_old_", StringComparison.Ordinal));
        Assert.Equal(2, mixer.Update(TimeSpan.FromSeconds(1)).Count);

        IReadOnlyList<AudioMixSample> completed = mixer.Update(TimeSpan.FromSeconds(1));
        Assert.Single(completed);
        Assert.Equal("bgm", completed[0].TrackId);
    }

    [Fact]
    public void StopFadeRemovesTrackAtZero()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("voice", "audio/line.ogg", AudioChannel.Voice));

        mixer.Stop(new AudioStopRequest("voice", 0.5));

        Assert.Single(mixer.Update(TimeSpan.FromSeconds(0.25)));
        Assert.Empty(mixer.Update(TimeSpan.FromSeconds(0.25)));
    }

    [Fact]
    public void VoiceDuckingMatchesLegacyExponentialStep()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("bgm", "audio/theme.ogg", AudioChannel.Bgm));
        mixer.Play(new AudioPlayRequest("voice", "audio/line.ogg", AudioChannel.Voice));

        IReadOnlyList<AudioMixSample> mix = mixer.Update(TimeSpan.FromSeconds(0.1));
        AudioMixSample bgm = Assert.Single(mix, sample => sample.Channel == AudioChannel.Bgm);

        Assert.Equal(0.79, mixer.CurrentDuck, precision: 6);
        Assert.Equal(0.7 * 0.79, bgm.EffectiveVolume, precision: 6);
    }

    [Fact]
    public void LegacyDuckingChecksOnlyPrimaryVoiceTrack()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("voice", "audio/old.ogg", AudioChannel.Voice));
        mixer.Play(new AudioPlayRequest("voice", "audio/new.ogg", AudioChannel.Voice, crossfadeSeconds: 2));
        mixer.Complete("voice");

        mixer.Update(TimeSpan.FromSeconds(0.1));

        Assert.Equal(1, mixer.CurrentDuck, precision: 6);
        Assert.Contains(mixer.Tracks, track => track.Id.StartsWith("voice_old_", StringComparison.Ordinal));
    }

    [Fact]
    public void OneShotsUseStableUniqueIdentifiers()
    {
        AudioMixerState mixer = new();

        string first = mixer.PlayOneShot("audio/click.ogg");
        string second = mixer.PlayOneShot("audio/click.ogg");

        Assert.Equal("sfx_1", first);
        Assert.Equal("sfx_2", second);
        Assert.Equal(2, mixer.Tracks.Count);
        Assert.All(mixer.Tracks, track => Assert.False(track.Looping));
    }

    [Fact]
    public void SnapshotRestoresFadesAndSequenceExactly()
    {
        AudioMixerState original = new();
        original.Play(new AudioPlayRequest("bgm", "audio/theme.ogg", AudioChannel.Bgm, fadeInSeconds: 2));
        original.Update(TimeSpan.FromSeconds(0.25));
        string firstOneShot = original.PlayOneShot("audio/click.ogg");

        AudioMixerState restored = AudioMixerState.Restore(original.Capture());
        string originalNextOneShot = original.PlayOneShot("audio/click.ogg");
        string restoredNextOneShot = restored.PlayOneShot("audio/click.ogg");

        Assert.Equal("sfx_1", firstOneShot);
        Assert.Equal("sfx_2", originalNextOneShot);
        Assert.Equal(originalNextOneShot, restoredNextOneShot);
        Assert.Equal(original.Update(TimeSpan.FromSeconds(0.25)), restored.Update(TimeSpan.FromSeconds(0.25)));
    }

    [Fact]
    public void TrackSnapshotsAreOrderedAndRestoreRejectsUntrustedInvalidState()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("voice", "audio/line.ogg", AudioChannel.Voice));
        mixer.Play(new AudioPlayRequest("bgm", "audio/theme.ogg", AudioChannel.Bgm));

        Assert.Equal(["bgm", "voice"], mixer.Capture().Tracks.Select(static track => track.Id));

        AudioMixerSnapshot invalid = mixer.Capture() with
        {
            Tracks =
            [
                new AudioTrackSnapshot(
                    "bad",
                    "audio/bad.ogg",
                    AudioChannel.Sfx,
                    double.NaN,
                    1,
                    0,
                    Playing: true,
                    Looping: false),
            ],
        };
        Assert.Throws<ArgumentException>(() => AudioMixerState.Restore(invalid));
    }

    [Fact]
    public void RejectsUnknownChannelBeforeItCanReachABackend()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AudioPlayRequest("bad", "audio/bad.ogg", (AudioChannel)99));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidDurations(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AudioPlayRequest("bgm", "audio/theme.ogg", AudioChannel.Bgm, fadeInSeconds: seconds));
    }

    [Fact]
    public void ChannelSettingsClampLikeLegacyMixer()
    {
        AudioMixerState mixer = new();
        mixer.SetChannelVolumes(master: -1, bgm: 2, sfx: 0.25, voice: 0.75);

        Assert.Equal(0, mixer.MasterVolume);
        Assert.Equal(1, mixer.BgmVolume);
        Assert.Equal(0.25, mixer.SfxVolume);
        Assert.Equal(0.75, mixer.VoiceVolume);
    }
}
