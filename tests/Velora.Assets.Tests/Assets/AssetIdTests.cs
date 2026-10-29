namespace Velora.Assets.Tests.Assets;

public sealed class AssetIdTests
{
    [Fact]
    public void TryCreateNormalizesCrossPlatformSeparatorsAndBenignParentSegments()
    {
        var success = AssetId.TryCreate(
            @"images\characters\mira\..\hero.png",
            out var assetId,
            out var error);

        Assert.True(success);
        Assert.Equal(AssetPathError.None, error);
        Assert.Equal("images/characters/hero.png", assetId.Value);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("images/../../secret")]
    [InlineData("/etc/passwd")]
    [InlineData(@"C:\Windows\system.ini")]
    [InlineData(@"C:/Windows/system.ini")]
    [InlineData(@"\\server\share\asset.png")]
    public void TryCreateRejectsTraversalAndPortableRootedPaths(string candidate)
    {
        Assert.False(AssetId.TryCreate(candidate, out _, out _));
    }

    [Fact]
    public void EqualityIsOrdinalAndCaseSensitive()
    {
        var upper = AssetId.Parse("Images/Hero.png");
        var lower = AssetId.Parse("images/hero.png");

        Assert.NotEqual(upper, lower);
        Assert.True(upper < lower);
    }
}
