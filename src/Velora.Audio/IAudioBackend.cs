using System.Collections.ObjectModel;

namespace Velora.Audio;

/// <summary>
/// Native backends own device/decoder resources and apply deterministic mix
/// samples produced by <see cref="AudioMixerState"/>. Expected device, asset,
/// and decoder failures cross this boundary as typed results, not exceptions.
/// </summary>
public interface IAudioBackend : IDisposable
{
    bool IsInitialized { get; }

    AudioBackendResult Initialize();

    AudioBackendResult Open(AudioTrackSnapshot track);

    /// <summary>
    /// Applies one deterministic mix frame and polls native completion/errors.
    /// The caller removes every completed ID from <see cref="AudioMixerState"/>.
    /// </summary>
    AudioBackendFrameResult Apply(IReadOnlyList<AudioMixSample> mix);

    AudioBackendTimeResult GetDuration(string trackId);

    AudioBackendTimeResult GetPosition(string trackId);

    AudioBackendResult Seek(string trackId, TimeSpan position);

    AudioBackendResult Close(string trackId);

    AudioBackendResult CloseAll();

    /// <summary>Pauses device playback while retaining resumable track resources.</summary>
    AudioBackendResult SuspendPlayback();

    /// <summary>Resumes playback after host suspension when audio focus permits it.</summary>
    AudioBackendResult ResumePlayback();

    /// <summary>Applies a mobile/desktop audio-focus interruption notification.</summary>
    AudioBackendResult HandleInterruption(AudioInterruption interruption);
}

public enum AudioInterruption
{
    Began,
    EndedMayResume,
    EndedMustRemainPaused,
}

public enum AudioBackendErrorSeverity
{
    Warning,
    Error,
}

public enum AudioBackendErrorCode
{
    NotInitialized,
    DeviceUnavailable,
    MissingAsset,
    UnsupportedFormat,
    DecodeFailure,
    InvalidTrack,
    SeekUnsupported,
    NativeFailure,
}

/// <summary>An expected, actionable backend failure detached from native exception types.</summary>
public sealed record AudioBackendError
{
    public AudioBackendError(
        AudioBackendErrorCode code,
        AudioBackendErrorSeverity severity,
        string operation,
        string message,
        string? trackId = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown audio backend error code.");
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown audio backend error severity.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (trackId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        }

        Code = code;
        Severity = severity;
        Operation = operation;
        Message = message;
        TrackId = trackId;
    }

    public AudioBackendErrorCode Code { get; }

    public AudioBackendErrorSeverity Severity { get; }

    public string Operation { get; }

    public string Message { get; }

    public string? TrackId { get; }
}

public sealed record AudioBackendResult
{
    private AudioBackendResult(AudioBackendError? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public AudioBackendError? Error { get; }

    public static AudioBackendResult Success { get; } = new((AudioBackendError?)null);

    public static AudioBackendResult Failure(AudioBackendError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AudioBackendResult(error);
    }
}

/// <summary>One non-negative duration/cursor query result.</summary>
public sealed record AudioBackendTimeResult
{
    private AudioBackendTimeResult(TimeSpan? value, AudioBackendError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Value.HasValue && Error is null;

    public TimeSpan? Value { get; }

    public AudioBackendError? Error { get; }

    public static AudioBackendTimeResult Success(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Audio time must not be negative.");
        }

        return new AudioBackendTimeResult(value, null);
    }

    public static AudioBackendTimeResult Failure(AudioBackendError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AudioBackendTimeResult(null, error);
    }
}

/// <summary>A deterministic completion/error poll returned once per applied mix frame.</summary>
public sealed record AudioBackendFrameResult
{
    public AudioBackendFrameResult(
        IEnumerable<string>? completedTrackIds = null,
        IEnumerable<AudioBackendError>? errors = null)
    {
        string[] completed = (completedTrackIds ?? [])
            .Select(static id =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(id);
                return id;
            })
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        for (int index = 1; index < completed.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(completed[index - 1], completed[index]))
            {
                throw new ArgumentException(
                    $"Completed track '{completed[index]}' occurs more than once.",
                    nameof(completedTrackIds));
            }
        }

        AudioBackendError[] orderedErrors = (errors ?? [])
            .Select(error => error ?? throw new ArgumentException(
                "An audio backend error must not be null.",
                nameof(errors)))
            .OrderBy(static error => error.TrackId, StringComparer.Ordinal)
            .ThenBy(static error => error.Code)
            .ThenBy(static error => error.Operation, StringComparer.Ordinal)
            .ThenBy(static error => error.Message, StringComparer.Ordinal)
            .ToArray();

        CompletedTrackIds = Array.AsReadOnly(completed);
        Errors = Array.AsReadOnly(orderedErrors);
    }

    public ReadOnlyCollection<string> CompletedTrackIds { get; }

    public ReadOnlyCollection<AudioBackendError> Errors { get; }

    public static AudioBackendFrameResult Empty { get; } = new();
}
