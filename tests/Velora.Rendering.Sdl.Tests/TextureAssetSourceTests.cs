using Velora.Assets;

namespace Velora.Rendering.Sdl.Tests;

public sealed class TextureAssetSourceTests
{
    [Fact]
    public void ResultRequiresAReadableStream()
    {
        using UnreadableStream stream = new();

        Assert.Throws<ArgumentException>(() => TextureAssetReadResult.Success(stream));
    }

    [Fact]
    public void AssetResolverAdapterUsesCanonicalLogicalOpen()
    {
        string directory = Path.Combine(Path.GetTempPath(), "velora-sdl-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string assetDirectory = Path.Combine(directory, "images");
        Directory.CreateDirectory(assetDirectory);
        string assetPath = Path.Combine(assetDirectory, "pixel.bin");
        File.WriteAllBytes(assetPath, [1, 2, 3]);
        try
        {
            AssetResolverTextureSource source = new(new AssetResolver(directory));

            TextureAssetReadResult result = source.OpenRead(AssetId.Parse("images/pixel.bin"));

            Assert.True(result.IsSuccess);
            using Stream stream = result.Stream!;
            Assert.Equal([1, 2, 3], ReadAll(stream));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
