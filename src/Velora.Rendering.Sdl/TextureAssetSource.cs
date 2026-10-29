using Velora.Assets;

namespace Velora.Rendering.Sdl;

/// <summary>
/// Supplies bounded logical assets to the SDL backend without exposing native
/// filesystem loading to VelScript or rendering state.
/// </summary>
public interface ITextureAssetSource
{
    TextureAssetReadResult OpenRead(AssetId assetId);
}

/// <summary>
/// An asset stream result. The backend owns and disposes a successful stream.
/// </summary>
public sealed class TextureAssetReadResult
{
    private TextureAssetReadResult(Stream? stream, string? error)
    {
        Stream = stream;
        Error = error;
    }

    public Stream? Stream { get; }

    public string? Error { get; }

    public bool IsSuccess => Stream is not null;

    public static TextureAssetReadResult Success(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The asset stream must be readable.", nameof(stream));
        }

        return new TextureAssetReadResult(stream, null);
    }

    public static TextureAssetReadResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new TextureAssetReadResult(null, error);
    }
}

/// <summary>Adapts the canonical Velora asset resolver to stream-only texture input.</summary>
public sealed class AssetResolverTextureSource : ITextureAssetSource
{
    private readonly AssetResolver _resolver;

    public AssetResolverTextureSource(AssetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public TextureAssetReadResult OpenRead(AssetId assetId)
    {
        AssetOpenResult result = _resolver.OpenRead(assetId.Value);
        return result.IsSuccess
            ? TextureAssetReadResult.Success(result.Stream!)
            : TextureAssetReadResult.Failure(
                result.Resolution.Diagnostic?.Message ?? $"Asset '{assetId}' could not be opened.");
    }
}

public sealed record TextureLoadFailure(string LogicalPath, string Message);
