namespace Velora.Assets;

/// <summary>
/// A read-only stream opened from a successful resolution. The caller owns and
/// must dispose <see cref="Stream"/>.
/// </summary>
public sealed record AssetOpenResult(Stream? Stream, AssetResolution Resolution)
{
    public bool IsSuccess => Stream is not null && Resolution.IsSuccess;
}
