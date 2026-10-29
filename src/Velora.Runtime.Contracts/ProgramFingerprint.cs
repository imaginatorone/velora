namespace Velora.Runtime.Contracts;

/// <summary>
/// Identifies the exact compiled program and compiler version used by a runtime state.
/// </summary>
public sealed record ProgramFingerprint
{
    /// <summary>The hexadecimal length of a SHA-256 content hash.</summary>
    public const int ContentHashLength = 64;

    /// <summary>Creates a fingerprint from a compiler version and SHA-256 content hash.</summary>
    public ProgramFingerprint(string compilerVersion, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (contentHash.Length != ContentHashLength || !contentHash.All(IsAsciiHexDigit))
        {
            throw new ArgumentException(
                $"The content hash must contain exactly {ContentHashLength} hexadecimal characters.",
                nameof(contentHash));
        }

        CompilerVersion = compilerVersion;
        ContentHash = contentHash.ToLowerInvariant();
    }

    /// <summary>Gets the compiler version included in the fingerprint identity.</summary>
    public string CompilerVersion { get; }

    /// <summary>Gets the canonical lowercase SHA-256 hash of the compiled content.</summary>
    public string ContentHash { get; }

    private static bool IsAsciiHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
