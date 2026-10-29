namespace Velora.Runtime.Contracts;

/// <summary>Serializable audio intent/state without native backend resources.</summary>
public sealed record AudioTrackState
{
    public string Channel { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public double Position { get; init; }

    public float Volume { get; init; } = 1.0f;

    public bool Playing { get; init; }
}
