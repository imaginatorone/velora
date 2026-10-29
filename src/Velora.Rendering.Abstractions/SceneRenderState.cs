using System.Collections.ObjectModel;
using System.Numerics;

namespace Velora.Rendering;

public sealed record SpriteRenderItem
{
    public SpriteRenderItem(
        string id,
        string assetPath,
        Vector2 position,
        Vector2 scale,
        double alpha = 1,
        double rotationDegrees = 0,
        int zOrder = 0,
        bool flipped = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Sprite position must be finite.");
        }

        if (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Sprite scale must be finite.");
        }

        if (!double.IsFinite(alpha) || alpha is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "Sprite alpha must be between zero and one.");
        }

        if (!double.IsFinite(rotationDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees), rotationDegrees, "Rotation must be finite.");
        }

        Id = id;
        AssetPath = assetPath;
        Position = position;
        Scale = scale;
        Alpha = alpha;
        RotationDegrees = rotationDegrees;
        ZOrder = zOrder;
        Flipped = flipped;
    }

    public string Id { get; }

    public string AssetPath { get; }

    /// <summary>Normalized logical position. The legacy backend ignores Y for sprites.</summary>
    public Vector2 Position { get; }

    public Vector2 Scale { get; }

    public double Alpha { get; }

    public double RotationDegrees { get; }

    public int ZOrder { get; }

    public bool Flipped { get; }
}

/// <summary>
/// Describes the two background layers used while a scene transition is in progress.
/// Progress zero shows only <see cref="PreviousAssetPath"/>; progress one shows only
/// <see cref="CurrentAssetPath"/>.
/// </summary>
public readonly record struct BackgroundRenderState
{
    public BackgroundRenderState(
        string? previousAssetPath,
        string? currentAssetPath,
        double transitionProgress)
    {
        if (!double.IsFinite(transitionProgress) || transitionProgress is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionProgress),
                transitionProgress,
                "Background transition progress must be between zero and one.");
        }

        PreviousAssetPath = previousAssetPath;
        CurrentAssetPath = currentAssetPath;
        TransitionProgress = transitionProgress;
    }

    public string? PreviousAssetPath { get; }

    public string? CurrentAssetPath { get; }

    public double TransitionProgress { get; }
}

public sealed class SceneRenderState
{
    private readonly ReadOnlyCollection<SpriteRenderItem> _sprites;

    public SceneRenderState(string? backgroundAssetPath, IEnumerable<SpriteRenderItem> sprites)
        : this(new BackgroundRenderState(null, backgroundAssetPath, 1), sprites)
    {
    }

    public SceneRenderState(BackgroundRenderState background, IEnumerable<SpriteRenderItem> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        Background = background;
        _sprites = Array.AsReadOnly(
        [
            .. sprites.Select((sprite, index) => (sprite, index))
                .OrderBy(item => item.sprite.ZOrder)
                .ThenBy(item => item.index)
                .Select(item => item.sprite),
        ]);
    }

    /// <summary>The explicit background transition contract for this frame.</summary>
    public BackgroundRenderState Background { get; }

    /// <summary>
    /// The current background path. Retained for source compatibility with the
    /// pre-transition scene contract.
    /// </summary>
    public string? BackgroundAssetPath => Background.CurrentAssetPath;

    public IReadOnlyList<SpriteRenderItem> Sprites => _sprites;
}

public static class LegacySpriteLayout
{
    public static RectD CalculateBounds(
        PixelSize texture,
        SpriteRenderItem sprite,
        LogicalSize logical)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        if (!texture.IsValid)
        {
            throw new ArgumentException("A valid texture size is required.", nameof(texture));
        }

        if (!logical.IsValid)
        {
            throw new ArgumentException("A valid logical size is required.", nameof(logical));
        }

        double baseScale = (logical.Height * 0.8) / texture.Height;
        double drawWidth = texture.Width * baseScale * sprite.Scale.X;
        double drawHeight = texture.Height * baseScale * sprite.Scale.Y;
        double drawX = (sprite.Position.X * logical.Width) - (drawWidth * 0.5);
        double drawY = logical.Height - drawHeight;
        return new RectD(drawX, drawY, drawWidth, drawHeight);
    }
}
