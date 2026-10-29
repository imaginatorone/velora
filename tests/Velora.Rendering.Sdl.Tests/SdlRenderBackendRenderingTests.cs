using System.Numerics;
using Velora.Rendering;
using Velora.Rendering.Sdl.Tests.TestSupport;

namespace Velora.Rendering.Sdl.Tests;

public sealed class SdlRenderBackendRenderingTests
{
    [Fact]
    public void CrossfadesFromOpaquePreviousBackgroundToProgressAlphaCurrentBackground()
    {
        FakeTextureAssetSource source = new();
        source.Add("backgrounds/previous.png", 1);
        source.Add("backgrounds/current.png", 2);
        FakeSdlNativeApi api = new();
        api.LoadedTextureSizes.Enqueue(new PixelSize(1280, 720));
        api.LoadedTextureSizes.Enqueue(new PixelSize(1280, 720));
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        SceneRenderState scene = new(
            new BackgroundRenderState(
                "backgrounds/previous.png",
                "backgrounds/current.png",
                transitionProgress: 0.25),
            []);

        backend.Render(new RenderFrame(scene, LogicalSize.VeloraDefault, TimeSpan.Zero));

        Assert.Equal(["backgrounds/previous.png", "backgrounds/current.png"], source.OpenedPaths);
        Assert.Equal([4, 5, 3], api.DrawCalls.Select(static call => call.Texture));
        Assert.Equal(
            [(4, byte.MaxValue), (4, byte.MaxValue), (5, (byte)63), (5, byte.MaxValue), (3, byte.MaxValue)],
            api.AlphaChanges);
    }

    [Fact]
    public void MissingBackgroundUsesStableLegacyProceduralBandsAndGrid()
    {
        const string path = "backgrounds/missing.png";
        FakeTextureAssetSource source = new();
        source.Fail(path, "not in bundle");
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        RenderFrame frame = new(
            new SceneRenderState(path, []),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero);

        backend.Render(frame);
        backend.Render(frame);

        Assert.Equal([path], source.OpenedPaths);
        Assert.Equal(new ProceduralBackgroundPalette(50, 63, 77), ProceduralBackground.CreatePalette(path));
        Assert.Equal(48, api.FillCalls.Count);
        Assert.Equal(
            new FillCall(new NativeRectF(0, 0, 1280, 31), new ColorRgba(10, 14, 18)),
            api.FillCalls[0]);
        Assert.Equal(
            new FillCall(new NativeRectF(0, 690, 1280, 31), new ColorRgba(37, 44, 50)),
            api.FillCalls[23]);
        Assert.Equal(50, api.LineCalls.Count);
        Assert.Equal(new ColorRgba(172, 204, 157, 25), api.LineCalls[0].Color);
        Assert.Equal([true, false, true, false], api.DrawBlendChanges);
    }

    [Fact]
    public void DrawsBackgroundThenStableZOrderedSpritesAndLetterboxesTarget()
    {
        FakeTextureAssetSource source = new();
        source.Add("backgrounds/room.png", 1);
        source.Add("sprites/front.png", 2);
        source.Add("sprites/back.png", 3);
        FakeSdlNativeApi api = new() { OutputSize = new PixelSize(2560, 1080) };
        api.LoadedTextureSizes.Enqueue(new PixelSize(1920, 1080));
        api.LoadedTextureSizes.Enqueue(new PixelSize(400, 800));
        api.LoadedTextureSizes.Enqueue(new PixelSize(200, 400));
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        SceneRenderState scene = new(
            "backgrounds/room.png",
            [
                Sprite("front", z: 2),
                Sprite("back", z: -1),
            ]);

        backend.Render(new RenderFrame(scene, LogicalSize.VeloraDefault, TimeSpan.Zero));

        Assert.Equal(
            ["backgrounds/room.png", "sprites/back.png", "sprites/front.png"],
            source.OpenedPaths);
        Assert.Collection(
            api.DrawCalls,
            draw => Assert.Equal(new NativeRectF(0, 0, 1280, 720), draw.Destination),
            draw => Assert.Equal(new NativeRectF(496, 144, 288, 576), draw.Destination),
            draw => Assert.Equal(new NativeRectF(496, 144, 288, 576), draw.Destination),
            draw => Assert.Equal(new NativeRectF(320, 0, 1920, 1080), draw.Destination));
        Assert.Equal(new RectD(320, 0, 1920, 1080), backend.LastLayout?.Viewport);
    }

    [Fact]
    public void PreservesInsertionOrderForEqualZAndMapsAlphaRotationAndFlip()
    {
        FakeTextureAssetSource source = new();
        source.Add("sprites/first.png", 1);
        source.Add("sprites/second.png", 2);
        FakeSdlNativeApi api = new();
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 100));
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 100));
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        SpriteRenderItem first = Sprite("first", z: 3);
        SpriteRenderItem second = new(
            "second",
            "sprites/second.png",
            new Vector2(0.5f, 0.5f),
            Vector2.One,
            alpha: 0.5,
            rotationDegrees: 30,
            zOrder: 3,
            flipped: true);

        backend.Render(new RenderFrame(
            new SceneRenderState(null, [first, second]),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero));

        Assert.Equal(["sprites/first.png", "sprites/second.png"], source.OpenedPaths);
        DrawCall transformed = api.DrawCalls[1];
        Assert.Equal(30, transformed.RotationDegrees);
        Assert.True(transformed.Flipped);
        Assert.Contains(api.AlphaChanges, change => change.Texture == transformed.Texture && change.Alpha == 127);
    }

    [Fact]
    public void MissingAssetIsSkippedAndNegativelyCached()
    {
        FakeTextureAssetSource source = new();
        source.Fail("sprites/missing.png", "not in bundle");
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        RenderFrame frame = new(
            new SceneRenderState(null, [Sprite("missing", z: 0)]),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero);

        backend.Render(frame);
        backend.Render(frame);

        Assert.Equal(["sprites/missing.png"], source.OpenedPaths);
        Assert.Equal(2, api.DrawCalls.Count);
        TextureLoadFailure failure = Assert.Single(backend.TextureFailures);
        Assert.Equal("sprites/missing.png", failure.LogicalPath);
        Assert.Equal("not in bundle", failure.Message);
    }

    [Fact]
    public void FlushTextureCacheDestroysAssetsButKeepsLogicalTarget()
    {
        FakeTextureAssetSource source = new();
        source.Add("sprites/mira.png", 1);
        FakeSdlNativeApi api = new();
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 200));
        using SdlRenderBackend backend = new(source, api);
        Assert.True(backend.Initialize(Surface()));
        backend.Render(new RenderFrame(
            new SceneRenderState(null, [Sprite("mira", z: 0)]),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero));

        backend.FlushTextureCache();

        Assert.Equal([4], api.DestroyedTextures);
        Assert.True(backend.IsInitialized);
    }

    [Fact]
    public void BoundedCacheEvictsTheLeastRecentlyUsedTextureDeterministically()
    {
        FakeTextureAssetSource source = new();
        source.Add("sprites/a.png", 1);
        source.Add("sprites/b.png", 2);
        source.Add("sprites/c.png", 3);
        FakeSdlNativeApi api = new();
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 100));
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 100));
        api.LoadedTextureSizes.Enqueue(new PixelSize(100, 100));
        SdlRenderBackendOptions options = new() { TextureCacheCapacity = 2 };
        using SdlRenderBackend backend = new(source, api, options);
        Assert.True(backend.Initialize(Surface()));

        RenderSprite(backend, "a");
        RenderSprite(backend, "b");
        RenderSprite(backend, "a");
        RenderSprite(backend, "c");

        Assert.Equal(["sprites/a.png", "sprites/b.png", "sprites/c.png"], source.OpenedPaths);
        Assert.Equal([5], api.DestroyedTextures);
    }

    [Fact]
    public void NegativeEntriesShareTheBoundedLruAndHitsRefreshRecency()
    {
        FakeTextureAssetSource source = new();
        source.Fail("sprites/a.png", "missing a");
        source.Fail("sprites/b.png", "missing b");
        source.Fail("sprites/c.png", "missing c");
        FakeSdlNativeApi api = new();
        SdlRenderBackendOptions options = new() { TextureCacheCapacity = 2 };
        using SdlRenderBackend backend = new(source, api, options);
        Assert.True(backend.Initialize(Surface()));

        RenderSprite(backend, "a");
        RenderSprite(backend, "b");
        RenderSprite(backend, "a");
        RenderSprite(backend, "c");
        RenderSprite(backend, "b");

        Assert.Equal(
            ["sprites/a.png", "sprites/b.png", "sprites/c.png", "sprites/b.png"],
            source.OpenedPaths);
        Assert.Equal(["sprites/c.png", "sprites/b.png"], backend.TextureFailures.Select(static item => item.LogicalPath));
    }

    [Theory]
    [InlineData(101, 1, 100, 10_000)]
    [InlineData(101, 100, 1_000, 10_000)]
    public void RejectsDecodedTexturesBeyondDimensionOrPixelLimits(
        int width,
        int height,
        int maximumDimension,
        long maximumPixels)
    {
        FakeTextureAssetSource source = new();
        source.Add("sprites/oversize.png", 1);
        FakeSdlNativeApi api = new();
        api.LoadedTextureSizes.Enqueue(new PixelSize(width, height));
        SdlRenderBackendOptions options = new()
        {
            MaximumTextureDimension = maximumDimension,
            MaximumTexturePixels = maximumPixels,
        };
        using SdlRenderBackend backend = new(source, api, options);
        Assert.True(backend.Initialize(Surface()));

        RenderSprite(backend, "oversize");

        Assert.Equal([4], api.DestroyedTextures);
        TextureLoadFailure failure = Assert.Single(backend.TextureFailures);
        Assert.Contains($"{width}x{height}", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureLoadedTexture:4", api.Operations);
    }

    [Fact]
    public void RejectsAFrameWithASeparateLogicalContract()
    {
        FakeSdlNativeApi api = new();
        using SdlRenderBackend backend = new(new FakeTextureAssetSource(), api);
        Assert.True(backend.Initialize(Surface()));
        RenderFrame frame = new(
            new SceneRenderState(null, []),
            new LogicalSize(640, 360),
            TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => backend.Render(frame));
        Assert.Empty(api.DrawCalls);
    }

    private static SpriteRenderItem Sprite(string id, int z) => new(
        id,
        $"sprites/{id}.png",
        new Vector2(0.5f, 0.5f),
        Vector2.One,
        zOrder: z);

    private static void RenderSprite(SdlRenderBackend backend, string id) =>
        backend.Render(new RenderFrame(
            new SceneRenderState(null, [Sprite(id, z: 0)]),
            LogicalSize.VeloraDefault,
            TimeSpan.Zero));

    private static SurfaceDescriptor Surface() => new(new PixelSize(1280, 720));
}
