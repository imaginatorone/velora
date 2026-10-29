using Velora.Audio;
using Velora.Audio.Miniaudio;

namespace Velora.Audio.Miniaudio.Tests;

public sealed class MiniaudioBackendTests
{
    [Fact]
    public void InitializeVerifiesNativeAbiAndPinnedMiniaudioVersion()
    {
        FakeNativeApi native = new() { AbiVersion = 99 };
        using MiniaudioBackend backend = new(native);

        AudioBackendResult result = backend.Initialize();

        Assert.False(result.IsSuccess);
        Assert.Equal(AudioBackendErrorCode.NativeFailure, result.Error!.Code);
        Assert.Equal(0, native.EngineCreateCalls);
        Assert.False(backend.IsInitialized);
    }

    [Fact]
    public void InitializeIsIdempotentAndDisposeReleasesEngineOnce()
    {
        FakeNativeApi native = new();
        MiniaudioBackend backend = new(native, useNullDevice: true);

        Assert.True(backend.Initialize().IsSuccess);
        Assert.True(backend.Initialize().IsSuccess);
        Assert.True(backend.IsInitialized);
        backend.Dispose();
        backend.Dispose();

        Assert.Equal(1, native.EngineCreateCalls);
        Assert.Equal(1, native.EngineDestroyCalls);
        Assert.False(backend.IsInitialized);
    }

    [Fact]
    public void OpenUsesStreamingOnlyForBgmAndConfiguresSoundBeforeStart()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);
        AudioTrackSnapshot bgm = Track("bgm", AudioChannel.Bgm, looping: true, volume: 0.4);
        AudioTrackSnapshot voice = Track("voice", AudioChannel.Voice, looping: false, volume: 0.8);

        Assert.True(backend.Open(bgm).IsSuccess);
        Assert.True(backend.Open(voice).IsSuccess);

        Assert.Equal([true, false], native.OpenStreamingFlags);
        Assert.Equal(
            ["open:bgm.ogg", "loop:1", "volume:0.4", "start", "open:voice.ogg", "loop:0", "volume:0.8", "start"],
            native.SoundOperations);
    }

    [Fact]
    public void ReopeningTrackDestroysPriorSoundBeforeReplacement()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);

        Assert.True(backend.Open(Track("voice", AudioChannel.Voice)).IsSuccess);
        nint first = native.LastOpenedSound;
        Assert.True(backend.Open(Track("voice", AudioChannel.Voice)).IsSuccess);

        Assert.Contains(first, native.DestroyedSounds);
        Assert.NotEqual(first, native.LastOpenedSound);
    }

    [Fact]
    public void OpenFailureIsTypedAndDoesNotRetainNativeHandle()
    {
        FakeNativeApi native = new() { OpenResult = -7 };
        using MiniaudioBackend backend = Initialized(native);

        AudioBackendResult result = backend.Open(Track("voice", AudioChannel.Voice));

        Assert.False(result.IsSuccess);
        Assert.Equal(AudioBackendErrorCode.MissingAsset, result.Error!.Code);
        Assert.Equal(AudioBackendErrorSeverity.Warning, result.Error.Severity);
        Assert.Empty(native.DestroyedSounds);
    }

    [Fact]
    public void ConfigurationInteropFailureReleasesOpenedSoundExactlyOnce()
    {
        FakeNativeApi native = new() { ThrowOnVolume = true };
        using MiniaudioBackend backend = Initialized(native);
        AudioBackendResult result = backend.Open(Track("voice", AudioChannel.Voice));
        Assert.False(result.IsSuccess);
        Assert.Equal(AudioBackendErrorCode.NativeFailure, result.Error!.Code);
        Assert.Equal([native.LastOpenedSound], native.DestroyedSounds);
        backend.CloseAll();
        Assert.Single(native.DestroyedSounds);
    }

    [Fact]
    public void ApplySetsFinalMixerGainAndReportsCompletionDeterministically()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);
        Assert.True(backend.Open(Track("voice", AudioChannel.Voice)).IsSuccess);
        native.AtEnd = true;

        AudioBackendFrameResult frame = backend.Apply(
        [
            new AudioMixSample("voice", AudioChannel.Voice, 0.25, Looping: false),
        ]);

        Assert.Equal(["voice"], frame.CompletedTrackIds);
        Assert.Empty(frame.Errors);
        Assert.Equal(0.25f, native.LastVolume);
        Assert.Single(native.DestroyedSounds);
    }

    [Fact]
    public void ApplyClosesTracksRemovedByMixerFadeCompletion()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);
        Assert.True(backend.Open(Track("voice", AudioChannel.Voice)).IsSuccess);

        AudioBackendFrameResult frame = backend.Apply([]);

        Assert.Empty(frame.CompletedTrackIds);
        Assert.Empty(frame.Errors);
        Assert.Single(native.DestroyedSounds);
    }

    [Fact]
    public void ApplyRejectsDuplicateAndUnknownMixTracksWithoutThrowing()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);

        AudioBackendFrameResult frame = backend.Apply(
        [
            new AudioMixSample("missing", AudioChannel.Sfx, 1, Looping: false),
            new AudioMixSample("missing", AudioChannel.Sfx, 1, Looping: false),
        ]);

        Assert.Equal(2, frame.Errors.Count);
        Assert.All(frame.Errors, error => Assert.Equal(AudioBackendErrorCode.InvalidTrack, error.Code));
    }

    [Fact]
    public void DurationPositionAndSeekUseOpaqueNativeSound()
    {
        FakeNativeApi native = new() { DurationSeconds = 12.5, CursorSeconds = 3.25 };
        using MiniaudioBackend backend = Initialized(native);
        Assert.True(backend.Open(Track("bgm", AudioChannel.Bgm)).IsSuccess);

        Assert.Equal(TimeSpan.FromSeconds(12.5), backend.GetDuration("bgm").Value);
        Assert.Equal(TimeSpan.FromSeconds(3.25), backend.GetPosition("bgm").Value);
        Assert.True(backend.Seek("bgm", TimeSpan.FromSeconds(8)).IsSuccess);
        Assert.Equal(8, native.LastSeekSeconds);
    }

    [Fact]
    public void HostSuspensionAndInterruptionHaveIndependentResumeGates()
    {
        FakeNativeApi native = new();
        using MiniaudioBackend backend = Initialized(native);

        Assert.True(backend.HandleInterruption(AudioInterruption.Began).IsSuccess);
        Assert.True(backend.SuspendPlayback().IsSuccess);
        Assert.True(backend.HandleInterruption(AudioInterruption.EndedMayResume).IsSuccess);
        Assert.Equal(1, native.EngineSuspendCalls);
        Assert.Equal(0, native.EngineResumeCalls);

        Assert.True(backend.ResumePlayback().IsSuccess);
        Assert.Equal(1, native.EngineResumeCalls);
    }

    [Fact]
    public void OperationsBeforeInitializeReturnTypedFailure()
    {
        using MiniaudioBackend backend = new(new FakeNativeApi());

        AudioBackendResult open = backend.Open(Track("voice", AudioChannel.Voice));
        AudioBackendFrameResult apply = backend.Apply([]);

        Assert.Equal(AudioBackendErrorCode.NotInitialized, open.Error!.Code);
        Assert.Equal(AudioBackendErrorCode.NotInitialized, Assert.Single(apply.Errors).Code);
    }

    private static MiniaudioBackend Initialized(FakeNativeApi native)
    {
        MiniaudioBackend backend = new(native, useNullDevice: true);
        Assert.True(backend.Initialize().IsSuccess);
        return backend;
    }

    private static AudioTrackSnapshot Track(
        string id,
        AudioChannel channel,
        bool looping = false,
        double volume = 1) => new(
            id,
            $"{id}.ogg",
            channel,
            volume,
            TargetVolume: volume,
            FadePerSecond: 0,
            Playing: true,
            Looping: looping);

    private sealed class FakeNativeApi : IMiniaudioNativeApi
    {
        private nint _nextHandle = 10;

        public uint AbiVersion { get; init; } = MiniaudioBackend.ExpectedAbiVersion;

        public string Version { get; init; } = MiniaudioBackend.ExpectedMiniaudioVersion;

        public int OpenResult { get; init; }

        public bool AtEnd { get; set; }

        public double DurationSeconds { get; init; }

        public double CursorSeconds { get; init; }

        public int EngineCreateCalls { get; private set; }

        public int EngineDestroyCalls { get; private set; }

        public int EngineSuspendCalls { get; private set; }

        public int EngineResumeCalls { get; private set; }

        public List<bool> OpenStreamingFlags { get; } = [];

        public List<string> SoundOperations { get; } = [];

        public List<nint> DestroyedSounds { get; } = [];

        public nint LastOpenedSound { get; private set; }

        public float LastVolume { get; private set; }

        public bool ThrowOnVolume { get; init; }

        public double LastSeekSeconds { get; private set; }

        public uint GetAbiVersion() => AbiVersion;

        public string GetMiniaudioVersion() => Version;

        public string DescribeResult(int result) => $"fake result {result}";

        public int EngineCreate(uint sampleRate, uint channels, bool noDevice, out nint engine)
        {
            Assert.Equal(48_000u, sampleRate);
            Assert.Equal(2u, channels);
            Assert.True(noDevice);
            EngineCreateCalls++;
            engine = ++_nextHandle;
            return 0;
        }

        public void EngineDestroy(nint engine)
        {
            Assert.NotEqual(0, engine);
            EngineDestroyCalls++;
        }

        public int EngineSuspend(nint engine)
        {
            EngineSuspendCalls++;
            return 0;
        }

        public int EngineResume(nint engine)
        {
            EngineResumeCalls++;
            return 0;
        }

        public int SoundOpenFile(nint engine, string path, bool streaming, out nint sound)
        {
            OpenStreamingFlags.Add(streaming);
            SoundOperations.Add($"open:{path}");
            if (OpenResult != 0)
            {
                sound = 0;
                return OpenResult;
            }

            sound = ++_nextHandle;
            LastOpenedSound = sound;
            return 0;
        }

        public void SoundDestroy(nint sound) => DestroyedSounds.Add(sound);

        public int SoundStart(nint sound)
        {
            SoundOperations.Add("start");
            return 0;
        }

        public int SoundStop(nint sound)
        {
            SoundOperations.Add("stop");
            return 0;
        }

        public int SoundSetVolume(nint sound, float volume)
        {
            if (ThrowOnVolume)
            {
                throw new EntryPointNotFoundException("Injected interop failure");
            }

            LastVolume = volume;
            SoundOperations.Add($"volume:{volume:G}");
            return 0;
        }

        public int SoundSetLooping(nint sound, bool looping)
        {
            SoundOperations.Add($"loop:{(looping ? 1 : 0)}");
            return 0;
        }

        public int SoundAtEnd(nint sound, out bool atEnd)
        {
            atEnd = AtEnd;
            return 0;
        }

        public int SoundGetDurationSeconds(nint sound, out double seconds)
        {
            seconds = DurationSeconds;
            return 0;
        }

        public int SoundGetCursorSeconds(nint sound, out double seconds)
        {
            seconds = CursorSeconds;
            return 0;
        }

        public int SoundSeekSeconds(nint sound, double seconds)
        {
            LastSeekSeconds = seconds;
            return 0;
        }
    }
}
