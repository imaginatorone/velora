using Velora.Assets;
using Velora.Rendering.Sdl;

namespace Velora.Rendering.Sdl.Tests.TestSupport;

internal sealed class FakeTextureAssetSource : ITextureAssetSource
{
    private readonly Dictionary<string, byte[]> _assets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);

    public List<string> OpenedPaths { get; } = [];

    public void Add(string path, params byte[] data) => _assets.Add(path, data.Length == 0 ? [0] : data);

    public void Fail(string path, string message) => _failures.Add(path, message);

    public TextureAssetReadResult OpenRead(AssetId assetId)
    {
        OpenedPaths.Add(assetId.Value);
        if (_assets.TryGetValue(assetId.Value, out byte[]? data))
        {
            return TextureAssetReadResult.Success(new MemoryStream(data, writable: false));
        }

        return TextureAssetReadResult.Failure(
            _failures.TryGetValue(assetId.Value, out string? message)
                ? message
                : $"Missing fake asset '{assetId}'.");
    }
}
