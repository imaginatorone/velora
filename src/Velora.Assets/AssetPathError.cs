namespace Velora.Assets;

/// <summary>Describes why a logical asset identifier was rejected.</summary>
public enum AssetPathError
{
    None,
    Empty,
    TooLong,
    Rooted,
    EscapesRoot,
    InvalidCharacter,
}
