using System.Diagnostics;
using System.Runtime.InteropServices;
using Velora.Audio;

namespace Velora.Audio.Miniaudio;

/// <summary>
/// First-party miniaudio device adapter. Mixing policy remains in
/// <see cref="AudioMixerState"/>; this type owns only opaque native resources.
/// </summary>
public sealed class MiniaudioBackend : IAudioBackend
{
    internal const uint ExpectedAbiVersion = 1;
    internal const string ExpectedMiniaudioVersion = "0.11.25";

    private const uint SampleRate = 48_000;
    private const uint ChannelCount = 2;
    private readonly object _sync = new();
    private readonly IMiniaudioNativeApi _native;
    private readonly bool _useNullDevice;
    private readonly Dictionary<string, NativeTrack> _tracks = new(StringComparer.Ordinal);
    private nint _engine;
    private bool _disposed;
    private bool _suspendRequested;
    private bool _interruptionBlocksResume;
    private bool _enginePaused;

    public MiniaudioBackend()
        : this(PInvokeMiniaudioNativeApi.Instance, useNullDevice: false)
    {
    }

    internal MiniaudioBackend(IMiniaudioNativeApi native, bool useNullDevice = false)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
        _useNullDevice = useNullDevice;
    }

    public bool IsInitialized
    {
        get
        {
            lock (_sync)
            {
                return !_disposed && _engine != 0;
            }
        }
    }

    public AudioBackendResult Initialize()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return DisposedFailure(nameof(Initialize));
            }

            if (_engine != 0)
            {
                return AudioBackendResult.Success;
            }

            try
            {
                uint abiVersion = _native.GetAbiVersion();
                string miniaudioVersion = _native.GetMiniaudioVersion();
                if (abiVersion != ExpectedAbiVersion ||
                    !StringComparer.Ordinal.Equals(miniaudioVersion, ExpectedMiniaudioVersion))
                {
                    return Failure(
                        AudioBackendErrorCode.NativeFailure,
                        AudioBackendErrorSeverity.Error,
                        "initialize",
                        $"Native audio version mismatch: expected ABI {ExpectedAbiVersion} with " +
                        $"miniaudio {ExpectedMiniaudioVersion}, received ABI {abiVersion} with " +
                        $"miniaudio '{miniaudioVersion}'.");
                }

                int result = _native.EngineCreate(
                    SampleRate,
                    ChannelCount,
                    _useNullDevice,
                    out nint engine);
                if (result != 0 || engine == 0)
                {
                    if (engine != 0)
                    {
                        _native.EngineDestroy(engine);
                    }

                    return NativeFailure(result, "initialize", trackId: null);
                }

                _engine = engine;
                _suspendRequested = false;
                _interruptionBlocksResume = false;
                _enginePaused = false;
                return AudioBackendResult.Success;
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return InteropFailure("initialize", exception);
            }
        }
    }

    public AudioBackendResult Open(AudioTrackSnapshot track)
    {
        ArgumentNullException.ThrowIfNull(track);

        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireEngine("open", track.Id);
            if (unavailable is not null)
            {
                return unavailable;
            }

            AudioBackendResult? invalid = ValidateTrack(track);
            if (invalid is not null)
            {
                return invalid;
            }

            try
            {
                CloseCore(track.Id);
                nint sound = 0;
                try
                {
                    int result = _native.SoundOpenFile(
                        _engine,
                        track.AssetPath,
                        streaming: track.Channel == AudioChannel.Bgm,
                        out sound);
                    if (result != 0 || sound == 0)
                    {
                        return NativeFailure(result, "open", track.Id);
                    }

                    AudioBackendResult setup = ConfigureOpenedSound(sound, track);
                    if (!setup.IsSuccess)
                    {
                        return setup;
                    }

                    _tracks.Add(track.Id, new NativeTrack(sound, track.Looping));
                    sound = 0; // Ownership transferred only after configuration succeeds.
                    return AudioBackendResult.Success;
                }
                finally
                {
                    if (sound != 0)
                    {
                        _native.SoundDestroy(sound);
                    }
                }
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return InteropFailure("open", exception, track.Id);
            }
        }
    }

    public AudioBackendFrameResult Apply(IReadOnlyList<AudioMixSample> mix)
    {
        ArgumentNullException.ThrowIfNull(mix);

        lock (_sync)
        {
            if (_disposed || _engine == 0)
            {
                return new AudioBackendFrameResult(errors:
                [
                    new AudioBackendError(
                        AudioBackendErrorCode.NotInitialized,
                        AudioBackendErrorSeverity.Error,
                        "apply",
                        _disposed
                            ? "The audio backend has been disposed."
                            : "The audio backend is not initialized."),
                ]);
            }

            Dictionary<string, AudioMixSample> requested = new(StringComparer.Ordinal);
            List<AudioBackendError> errors = [];
            foreach (AudioMixSample sample in mix)
            {
                if (string.IsNullOrWhiteSpace(sample.TrackId) ||
                    !Enum.IsDefined(sample.Channel) ||
                    !double.IsFinite(sample.EffectiveVolume) ||
                    sample.EffectiveVolume is > float.MaxValue or < -float.MaxValue)
                {
                    errors.Add(new AudioBackendError(
                        AudioBackendErrorCode.InvalidTrack,
                        AudioBackendErrorSeverity.Warning,
                        "apply",
                        "A mix sample contains an invalid track ID, channel, or volume.",
                        string.IsNullOrWhiteSpace(sample.TrackId) ? null : sample.TrackId));
                    continue;
                }

                if (!requested.TryAdd(sample.TrackId, sample))
                {
                    errors.Add(new AudioBackendError(
                        AudioBackendErrorCode.InvalidTrack,
                        AudioBackendErrorSeverity.Warning,
                        "apply",
                        "The mix frame contains the same track more than once.",
                        sample.TrackId));
                }
            }

            try
            {
                foreach (string staleId in _tracks.Keys
                    .Where(id => !requested.ContainsKey(id))
                    .OrderBy(static id => id, StringComparer.Ordinal)
                    .ToArray())
                {
                    CloseCore(staleId);
                }

                List<string> completed = [];
                foreach ((string trackId, AudioMixSample sample) in requested
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!_tracks.TryGetValue(trackId, out NativeTrack? track))
                    {
                        errors.Add(new AudioBackendError(
                            AudioBackendErrorCode.InvalidTrack,
                            AudioBackendErrorSeverity.Warning,
                            "apply",
                            "The mixer referenced a track that is not open in the native backend.",
                            trackId));
                        continue;
                    }

                    int result = _native.SoundSetVolume(track.Handle, (float)sample.EffectiveVolume);
                    if (result != 0)
                    {
                        errors.Add(CreateNativeError(result, "set-volume", trackId));
                    }

                    if (track.Looping != sample.Looping)
                    {
                        result = _native.SoundSetLooping(track.Handle, sample.Looping);
                        if (result == 0)
                        {
                            track.Looping = sample.Looping;
                        }
                        else
                        {
                            errors.Add(CreateNativeError(result, "set-looping", trackId));
                        }
                    }

                    result = _native.SoundAtEnd(track.Handle, out bool atEnd);
                    if (result != 0)
                    {
                        errors.Add(CreateNativeError(result, "poll-completion", trackId));
                    }
                    else if (atEnd && !sample.Looping)
                    {
                        completed.Add(trackId);
                    }
                }

                foreach (string trackId in completed)
                {
                    CloseCore(trackId);
                }

                return new AudioBackendFrameResult(completed, errors);
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                errors.Add(CreateInteropError("apply", exception));
                return new AudioBackendFrameResult(errors: errors);
            }
        }
    }

    public AudioBackendTimeResult GetDuration(string trackId) =>
        QueryTime(trackId, "duration", _native.SoundGetDurationSeconds);

    public AudioBackendTimeResult GetPosition(string trackId) =>
        QueryTime(trackId, "position", _native.SoundGetCursorSeconds);

    public AudioBackendResult Seek(string trackId, TimeSpan position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        if (position < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Audio position must not be negative.");
        }

        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireTrack(trackId, "seek", out NativeTrack? track);
            if (unavailable is not null)
            {
                return unavailable;
            }

            try
            {
                int result = _native.SoundSeekSeconds(track!.Handle, position.TotalSeconds);
                return result == 0
                    ? AudioBackendResult.Success
                    : NativeFailure(result, "seek", trackId);
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return InteropFailure("seek", exception, trackId);
            }
        }
    }

    public AudioBackendResult Close(string trackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        lock (_sync)
        {
            if (_disposed)
            {
                return DisposedFailure("close", trackId);
            }

            try
            {
                CloseCore(trackId);
                return AudioBackendResult.Success;
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return InteropFailure("close", exception, trackId);
            }
        }
    }

    public AudioBackendResult CloseAll()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return DisposedFailure("close-all");
            }

            try
            {
                CloseAllCore();
                return AudioBackendResult.Success;
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return InteropFailure("close-all", exception);
            }
        }
    }

    public AudioBackendResult SuspendPlayback()
    {
        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireEngine("suspend", trackId: null);
            if (unavailable is not null)
            {
                return unavailable;
            }

            _suspendRequested = true;
            return SynchronizeEnginePause("suspend");
        }
    }

    public AudioBackendResult ResumePlayback()
    {
        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireEngine("resume", trackId: null);
            if (unavailable is not null)
            {
                return unavailable;
            }

            _suspendRequested = false;
            return SynchronizeEnginePause("resume");
        }
    }

    public AudioBackendResult HandleInterruption(AudioInterruption interruption)
    {
        if (!Enum.IsDefined(interruption))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interruption),
                interruption,
                "Unknown audio interruption state.");
        }

        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireEngine("interruption", trackId: null);
            if (unavailable is not null)
            {
                return unavailable;
            }

            _interruptionBlocksResume = interruption switch
            {
                AudioInterruption.Began => true,
                AudioInterruption.EndedMayResume => false,
                AudioInterruption.EndedMustRemainPaused => true,
                _ => throw new UnreachableException(),
            };
            return SynchronizeEnginePause("interruption");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                CloseAllCore();
                if (_engine != 0)
                {
                    _native.EngineDestroy(_engine);
                }
            }
            finally
            {
                _engine = 0;
                _disposed = true;
            }
        }
    }

    private delegate int TimeQuery(nint sound, out double seconds);

    private AudioBackendTimeResult QueryTime(string trackId, string operation, TimeQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        lock (_sync)
        {
            AudioBackendResult? unavailable = RequireTrack(trackId, operation, out NativeTrack? track);
            if (unavailable is not null)
            {
                return AudioBackendTimeResult.Failure(unavailable.Error!);
            }

            try
            {
                int result = query(track!.Handle, out double seconds);
                if (result != 0)
                {
                    return AudioBackendTimeResult.Failure(
                        CreateNativeError(result, operation, trackId));
                }

                if (!double.IsFinite(seconds) || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds)
                {
                    return AudioBackendTimeResult.Failure(new AudioBackendError(
                        AudioBackendErrorCode.NativeFailure,
                        AudioBackendErrorSeverity.Warning,
                        operation,
                        "The native backend returned an invalid audio time.",
                        trackId));
                }

                return AudioBackendTimeResult.Success(TimeSpan.FromSeconds(seconds));
            }
            catch (Exception exception) when (IsInteropLoadException(exception))
            {
                return AudioBackendTimeResult.Failure(CreateInteropError(operation, exception, trackId));
            }
        }
    }

    private AudioBackendResult ConfigureOpenedSound(nint sound, AudioTrackSnapshot track)
    {
        int result = _native.SoundSetLooping(sound, track.Looping);
        if (result != 0)
        {
            return NativeFailure(result, "set-looping", track.Id);
        }

        result = _native.SoundSetVolume(sound, (float)track.Volume);
        if (result != 0)
        {
            return NativeFailure(result, "set-volume", track.Id);
        }

        result = track.Playing ? _native.SoundStart(sound) : _native.SoundStop(sound);
        return result == 0
            ? AudioBackendResult.Success
            : NativeFailure(result, track.Playing ? "start" : "stop", track.Id);
    }

    private AudioBackendResult SynchronizeEnginePause(string operation)
    {
        bool shouldPause = _suspendRequested || _interruptionBlocksResume;
        if (shouldPause == _enginePaused)
        {
            return AudioBackendResult.Success;
        }

        try
        {
            int result = shouldPause
                ? _native.EngineSuspend(_engine)
                : _native.EngineResume(_engine);
            if (result != 0)
            {
                return NativeFailure(result, operation, trackId: null);
            }

            _enginePaused = shouldPause;
            return AudioBackendResult.Success;
        }
        catch (Exception exception) when (IsInteropLoadException(exception))
        {
            return InteropFailure(operation, exception);
        }
    }

    private AudioBackendResult? RequireEngine(string operation, string? trackId)
    {
        if (_disposed)
        {
            return DisposedFailure(operation, trackId);
        }

        return _engine == 0
            ? Failure(
                AudioBackendErrorCode.NotInitialized,
                AudioBackendErrorSeverity.Error,
                operation,
                "The audio backend is not initialized.",
                trackId)
            : null;
    }

    private AudioBackendResult? RequireTrack(
        string trackId,
        string operation,
        out NativeTrack? track)
    {
        AudioBackendResult? unavailable = RequireEngine(operation, trackId);
        if (unavailable is not null)
        {
            track = null;
            return unavailable;
        }

        if (!_tracks.TryGetValue(trackId, out track))
        {
            return Failure(
                AudioBackendErrorCode.InvalidTrack,
                AudioBackendErrorSeverity.Warning,
                operation,
                "The requested audio track is not open.",
                trackId);
        }

        return null;
    }

    private static AudioBackendResult? ValidateTrack(AudioTrackSnapshot track)
    {
        if (string.IsNullOrWhiteSpace(track.Id) ||
            string.IsNullOrWhiteSpace(track.AssetPath) ||
            !Enum.IsDefined(track.Channel) ||
            !double.IsFinite(track.Volume) ||
            track.Volume is > float.MaxValue or < -float.MaxValue)
        {
            return Failure(
                AudioBackendErrorCode.InvalidTrack,
                AudioBackendErrorSeverity.Warning,
                "open",
                "The audio track contains an invalid ID, asset path, channel, or volume.",
                string.IsNullOrWhiteSpace(track.Id) ? null : track.Id);
        }

        return null;
    }

    private void CloseCore(string trackId)
    {
        if (_tracks.Remove(trackId, out NativeTrack? track))
        {
            _native.SoundDestroy(track.Handle);
        }
    }

    private void CloseAllCore()
    {
        foreach (NativeTrack track in _tracks.Values)
        {
            _native.SoundDestroy(track.Handle);
        }

        _tracks.Clear();
    }

    private AudioBackendResult NativeFailure(int nativeResult, string operation, string? trackId) =>
        AudioBackendResult.Failure(CreateNativeError(nativeResult, operation, trackId));

    private AudioBackendError CreateNativeError(int result, string operation, string? trackId)
    {
        AudioBackendErrorCode code = result switch
        {
            -7 or -14 or -15 => AudioBackendErrorCode.MissingAsset,
            -10 or -20 or -31 or -33 or -100 => AudioBackendErrorCode.DecodeFailure,
            -25 or -29 when operation is "seek" or "duration" or "position" =>
                AudioBackendErrorCode.SeekUnsupported,
            -200 => AudioBackendErrorCode.UnsupportedFormat,
            -203 or -204 or -205 or -208 or -300 or -302 =>
                AudioBackendErrorCode.DeviceUnavailable,
            -2 or -3 or -5 => AudioBackendErrorCode.InvalidTrack,
            _ => AudioBackendErrorCode.NativeFailure,
        };
        AudioBackendErrorSeverity severity = trackId is null || code == AudioBackendErrorCode.DeviceUnavailable
            ? AudioBackendErrorSeverity.Error
            : AudioBackendErrorSeverity.Warning;
        string description;
        try
        {
            description = _native.DescribeResult(result);
        }
        catch (Exception exception) when (IsInteropLoadException(exception))
        {
            description = "native result description unavailable";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = "unknown native result";
        }

        return new AudioBackendError(
            code,
            severity,
            operation,
            $"Miniaudio operation failed: {description} (native result {result}).",
            trackId);
    }

    private static AudioBackendResult InteropFailure(
        string operation,
        Exception exception,
        string? trackId = null) =>
        AudioBackendResult.Failure(CreateInteropError(operation, exception, trackId));

    private static AudioBackendError CreateInteropError(
        string operation,
        Exception exception,
        string? trackId = null) => new(
            AudioBackendErrorCode.NativeFailure,
            AudioBackendErrorSeverity.Error,
            operation,
            $"The Velora native audio library could not be loaded or invoked ({exception.GetType().Name}).",
            trackId);

    private static AudioBackendResult DisposedFailure(string operation, string? trackId = null) =>
        Failure(
            AudioBackendErrorCode.NotInitialized,
            AudioBackendErrorSeverity.Error,
            operation,
            "The audio backend has been disposed.",
            trackId);

    private static AudioBackendResult Failure(
        AudioBackendErrorCode code,
        AudioBackendErrorSeverity severity,
        string operation,
        string message,
        string? trackId = null) =>
        AudioBackendResult.Failure(new AudioBackendError(
            code,
            severity,
            operation,
            message,
            trackId));

    private static bool IsInteropLoadException(Exception exception) =>
        exception is DllNotFoundException or
        EntryPointNotFoundException or
        BadImageFormatException or
        MarshalDirectiveException;

    private sealed class NativeTrack(nint handle, bool looping)
    {
        public nint Handle { get; } = handle;

        public bool Looping { get; set; } = looping;
    }
}
