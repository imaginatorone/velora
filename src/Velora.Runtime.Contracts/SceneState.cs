namespace Velora.Runtime.Contracts;

/// <summary>Captures the logical scene independently of a rendering backend.</summary>
public sealed record SceneState
{
    /// <summary>Creates a scene state. Sprites are normalized to z-order then ordinal identifier.</summary>
    public SceneState(string backgroundPath, IEnumerable<SpriteState> sprites)
    {
        ArgumentNullException.ThrowIfNull(backgroundPath);
        ArgumentNullException.ThrowIfNull(sprites);

        SpriteState[] ordered = sprites
            .OrderBy(sprite => ValidateSprite(sprite).ZOrder)
            .ThenBy(sprite => sprite.Id, StringComparer.Ordinal)
            .ToArray();

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpriteState sprite in ordered)
        {
            if (!identifiers.Add(sprite.Id))
            {
                throw new ArgumentException(
                    $"The sprite identifier '{sprite.Id}' occurs more than once.",
                    nameof(sprites));
            }
        }

        BackgroundPath = backgroundPath;
        Sprites = Array.AsReadOnly(ordered);
    }

    public string BackgroundPath { get; }

    public IReadOnlyList<SpriteState> Sprites { get; }

    private static SpriteState ValidateSprite(SpriteState? sprite)
    {
        return ContractCollections.ValidateSprite(sprite, nameof(sprite));
    }
}

/// <summary>Describes an optional presentation transition without backend handles.</summary>
public sealed record TransitionIntent
{
    /// <summary>Creates a transition intent.</summary>
    public TransitionIntent(string name, TimeSpan duration)
    {
        ContractCollections.RequireNonEmpty(name, nameof(name));
        ContractCollections.RequireNonNegative(duration, nameof(duration));
        Name = name;
        Duration = duration;
    }

    public string Name { get; }

    public TimeSpan Duration { get; }
}
