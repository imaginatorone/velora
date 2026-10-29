namespace Velora.Runtime;

/// <summary>Stable warning identifiers emitted for recoverable runtime conditions.</summary>
public static class RuntimeWarningCodes
{
    public const string MissingJumpTarget = "RUN001";
    public const string InvalidExpression = "RUN002";
    public const string MissingSprite = "RUN003";
    public const string InvalidTween = "RUN004";
    public const string UnknownEmotion = "RUN005";
    public const string UnknownParticlePreset = "RUN006";
    public const string UnsupportedIntent = "RUN007";
    public const string MissingScreen = "RUN008";
    public const string UnknownMetaCommand = "RUN009";
    public const string InvalidArgument = "RUN010";
    public const string ExecutionLimitReached = "RUN011";
}
