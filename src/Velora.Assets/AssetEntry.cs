namespace Velora.Assets;

/// <summary>A deterministic asset-database entry.</summary>
public sealed record AssetEntry(
    AssetId Id,
    string FullPath,
    long Length,
    AssetOrigin Origin,
    string? MountPrefix,
    bool IsSymbolicLink);
