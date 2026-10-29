using System.Numerics;
using Velora.Rendering;

namespace Velora.Rendering.Tests;

public sealed class SceneRenderStateTests
{
    [Fact]
    public void SortsSpritesByZAndPreservesInsertionOrderForTies()
    {
        SpriteRenderItem back = Sprite("back", z: -1);
        SpriteRenderItem first = Sprite("first", z: 2);
        SpriteRenderItem second = Sprite("second", z: 2);

        SceneRenderState scene = new("backgrounds/room.png", [first, back, second]);

        Assert.Equal(["back", "first", "second"], scene.Sprites.Select(sprite => sprite.Id));
        Assert.Equal(new BackgroundRenderState(null, "backgrounds/room.png", 1), scene.Background);
        Assert.Equal("backgrounds/room.png", scene.BackgroundAssetPath);
    }

    [Fact]
    public void PreservesExplicitBackgroundTransitionContract()
    {
        BackgroundRenderState background = new(
            "backgrounds/room.png",
            "backgrounds/garden.png",
            transitionProgress: 0.375);

        SceneRenderState scene = new(background, []);

        Assert.Equal(background, scene.Background);
        Assert.Equal("backgrounds/garden.png", scene.BackgroundAssetPath);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidBackgroundTransitionProgress(double progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BackgroundRenderState(null, null, progress));
    }

    [Fact]
    public void ReadbackDescriptorMakesBgraMemoryLayoutExplicit()
    {
        FrameReadbackDescriptor descriptor = FrameReadbackDescriptor.CreateBgra8TopDown(new PixelSize(2, 3));

        Assert.Equal(FramePixelFormat.Bgra8, descriptor.PixelFormat);
        Assert.Equal(FrameRowOrder.TopDown, descriptor.RowOrder);
        Assert.Equal(8, descriptor.RowPitch);
        Assert.Equal(24, descriptor.RequiredBytes);
        Assert.True(descriptor.IsTightlyPacked);
    }

    [Fact]
    public void LegacySpriteBoundsAreBottomAnchoredAndIgnoreY()
    {
        SpriteRenderItem lowY = new(
            "mira",
            "sprites/mira.png",
            new Vector2(0.25f, 0.1f),
            new Vector2(1.5f, 0.5f));
        SpriteRenderItem highY = new(
            "mira",
            "sprites/mira.png",
            new Vector2(0.25f, 0.9f),
            new Vector2(1.5f, 0.5f));

        RectD first = LegacySpriteLayout.CalculateBounds(new PixelSize(400, 800), lowY, LogicalSize.VeloraDefault);
        RectD second = LegacySpriteLayout.CalculateBounds(new PixelSize(400, 800), highY, LogicalSize.VeloraDefault);

        Assert.Equal(new RectD(104, 432, 432, 288), first);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC, 0xFF)]
    [InlineData("#123456", 0x12, 0x34, 0x56, 0xFF)]
    [InlineData("#12345678", 0x12, 0x34, 0x56, 0x78)]
    public void ParsesVelScriptColors(string text, byte r, byte g, byte b, byte a)
    {
        Assert.True(ColorRgba.TryParseVelScript(text, out ColorRgba color));
        Assert.Equal(new ColorRgba(r, g, b, a), color);
    }

    [Theory]
    [InlineData("#12")]
    [InlineData("#1234")]
    [InlineData("red")]
    [InlineData("#gggggg")]
    public void RejectsInvalidColors(string text)
    {
        Assert.False(ColorRgba.TryParseVelScript(text, out _));
    }

    private static SpriteRenderItem Sprite(string id, int z) => new(
        id,
        $"sprites/{id}.png",
        new Vector2(0.5f, 0.5f),
        Vector2.One,
        zOrder: z);
}
