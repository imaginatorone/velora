using System.Reflection;
using Velora.Audio;

namespace Velora.Audio.Tests;

public sealed class AudioBackendContractTests
{
    [Fact]
    public void ExpectedBackendFailuresAreTypedAndActionable()
    {
        AudioBackendError error = new(
            AudioBackendErrorCode.MissingAsset,
            AudioBackendErrorSeverity.Warning,
            "open",
            "The optional voice asset is missing.",
            "voice");

        AudioBackendResult result = AudioBackendResult.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.True(AudioBackendResult.Success.IsSuccess);
        Assert.Null(AudioBackendResult.Success.Error);
    }

    [Fact]
    public void FramePollNormalizesCompletionAndErrorsDeterministically()
    {
        AudioBackendError zeta = Error("zeta", AudioBackendErrorCode.NativeFailure, "apply");
        AudioBackendError alpha = Error("alpha", AudioBackendErrorCode.DecodeFailure, "decode");

        AudioBackendFrameResult result = new(["voice", "bgm"], [zeta, alpha]);

        Assert.Equal(["bgm", "voice"], result.CompletedTrackIds);
        Assert.Equal([alpha, zeta], result.Errors);
    }

    [Fact]
    public void FramePollRejectsDuplicateOrInvalidCompletionIds()
    {
        Assert.Throws<ArgumentException>(() => new AudioBackendFrameResult(["voice", "voice"]));
        Assert.Throws<ArgumentException>(() => new AudioBackendFrameResult([""]));
        Assert.Throws<ArgumentException>(() => new AudioBackendFrameResult(errors: [null!]));
    }

    [Fact]
    public void CompletionIdsProvideTheExplicitMixerRetirementPath()
    {
        AudioMixerState mixer = new();
        mixer.Play(new AudioPlayRequest("voice", "audio/line.ogg", AudioChannel.Voice));
        AudioBackendFrameResult frame = new(["voice"]);

        foreach (string trackId in frame.CompletedTrackIds)
        {
            Assert.True(mixer.Complete(trackId));
        }

        Assert.Empty(mixer.Tracks);
    }

    [Fact]
    public void TimelineResultsRequireNonNegativeValuesAndExclusiveErrorState()
    {
        AudioBackendTimeResult duration = AudioBackendTimeResult.Success(TimeSpan.FromSeconds(12.5));
        AudioBackendError error = Error("bgm", AudioBackendErrorCode.SeekUnsupported, "duration");
        AudioBackendTimeResult failed = AudioBackendTimeResult.Failure(error);

        Assert.True(duration.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(12.5), duration.Value);
        Assert.False(failed.IsSuccess);
        Assert.Null(failed.Value);
        Assert.Same(error, failed.Error);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AudioBackendTimeResult.Success(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void ErrorContractsRejectUnknownEnumsAndUnusableMessages()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioBackendError(
            (AudioBackendErrorCode)99,
            AudioBackendErrorSeverity.Error,
            "open",
            "failed"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioBackendError(
            AudioBackendErrorCode.NativeFailure,
            (AudioBackendErrorSeverity)99,
            "open",
            "failed"));
        Assert.Throws<ArgumentException>(() => new AudioBackendError(
            AudioBackendErrorCode.NativeFailure,
            AudioBackendErrorSeverity.Error,
            " ",
            "failed"));
    }

    [Fact]
    public void BackendInterfaceHasNoBooleanOrVoidOperationalResults()
    {
        MethodInfo[] operations = typeof(IAudioBackend).GetMethods()
            .Where(static method => method.Name != "get_IsInitialized" && method.Name != nameof(IDisposable.Dispose))
            .ToArray();

        Assert.NotEmpty(operations);
        Assert.All(operations, static method =>
        {
            Assert.NotEqual(typeof(bool), method.ReturnType);
            Assert.NotEqual(typeof(void), method.ReturnType);
        });
        Assert.Contains(operations, static method => method.ReturnType == typeof(AudioBackendFrameResult));
        Assert.Contains(operations, static method => method.ReturnType == typeof(AudioBackendTimeResult));
    }

    private static AudioBackendError Error(
        string trackId,
        AudioBackendErrorCode code,
        string operation) => new(
            code,
            AudioBackendErrorSeverity.Warning,
            operation,
            "backend warning",
            trackId);
}
