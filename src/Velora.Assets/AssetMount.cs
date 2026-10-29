namespace Velora.Assets;

/// <summary>Maps a logical prefix to an explicitly trusted, read-only content root.</summary>
public sealed record AssetMount
{
    public AssetMount(AssetId prefix, string rootDirectory)
    {
        if (!prefix.IsValid)
        {
            throw new ArgumentException("A mount prefix must be a valid asset identifier.", nameof(prefix));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Prefix = prefix;
        RootDirectory = rootDirectory;
    }

    public AssetId Prefix { get; }

    public string RootDirectory { get; }

    public static AssetMount Create(string prefix, string rootDirectory) =>
        new(AssetId.Parse(prefix), rootDirectory);
}
