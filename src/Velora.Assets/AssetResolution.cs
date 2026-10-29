using Velora.Assets.Diagnostics;

namespace Velora.Assets;

/// <summary>The canonical location of an asset, or one actionable failure diagnostic.</summary>
public sealed record AssetResolution(
    AssetId? AssetId,
    string? FullPath,
    AssetOrigin? Origin,
    string? MountPrefix,
    AssetDiagnostic? Diagnostic)
{
    public bool IsSuccess => AssetId is not null && FullPath is not null && Diagnostic is null;

    internal static AssetResolution Success(
        AssetId assetId,
        string fullPath,
        AssetOrigin origin,
        string? mountPrefix) =>
        new(assetId, fullPath, origin, mountPrefix, null);

    internal static AssetResolution Failure(
        AssetId? assetId,
        AssetDiagnostic diagnostic,
        string? fullPath = null,
        AssetOrigin? origin = null,
        string? mountPrefix = null) =>
        new(assetId, fullPath, origin, mountPrefix, diagnostic);
}
