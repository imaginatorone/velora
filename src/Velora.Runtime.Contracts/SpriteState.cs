namespace Velora.Runtime.Contracts;

/// <summary>Serializable presentation-neutral sprite state from a runtime snapshot.</summary>
public sealed record SpriteState
{
    public string Id { get; init; } = string.Empty;

    public string ImagePath { get; init; } = string.Empty;

    public float X { get; init; } = 0.5f;

    public float Y { get; init; } = 0.5f;

    public float Alpha { get; init; } = 1.0f;

    public float ScaleX { get; init; } = 1.0f;

    public float ScaleY { get; init; } = 1.0f;

    public float Rotation { get; init; }

    public int ZOrder { get; init; }

    public bool Flipped { get; init; }
}
