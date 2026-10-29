namespace Velora.Persistence;

/// <summary>Compatibility and untrusted-input bounds shared by VSAV and VPER codecs.</summary>
public static class PersistenceLimits
{
    public const int MaxFileBytes = 16 * 1024 * 1024;

    public const int MaxLegacyStringBytes = 4 * 1024 * 1024;

    public const int MaxVariables = 10_000;

    public const int MaxSprites = 1_000;

    public const int MaxAudioTracks = 128;

    internal const int MaxCborDepth = 32;

    internal const int MaxUnknownCollectionItems = 100_000;

    internal const int MaxPayloadBytes = MaxFileBytes - VersionedFile.HeaderLength;
}
