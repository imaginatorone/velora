using Velora.Platform;

namespace Velora.Platform.Tests;

public sealed class StoragePathTests
{
    [Theory]
    [InlineData("save_1.vsav", "save_1.vsav")]
    [InlineData("auto/save_2.vsav", "auto/save_2.vsav")]
    [InlineData("route\\flags.json", "route/flags.json")]
    public void AcceptsAndNormalizesSafeRelativeNames(string value, string expected)
    {
        Assert.True(StoragePath.TryParse(value, out StoragePath path));
        Assert.Equal(expected, path.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("C:\\save.vsav")]
    [InlineData("../escape")]
    [InlineData("safe/../escape")]
    [InlineData("./save")]
    [InlineData("double//slash")]
    [InlineData("trailing/")]
    public void RejectsUnsafeOrAmbiguousNames(string value)
    {
        Assert.False(StoragePath.TryParse(value, out _));
    }

    [Fact]
    public void DefaultValueIsNotAValidPath()
    {
        Assert.False(default(StoragePath).IsValid);
    }

    [Fact]
    public void StorageEntryRejectsDefaultPathAndNegativeLength()
    {
        Assert.Throws<ArgumentException>(() => new StorageEntry(default, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StorageEntry(StoragePath.Parse("save.vsav"), -1, null));
    }
}
