using Velora.Assets.Diagnostics;
using Velora.Assets.Paths;

namespace Velora.Assets;

/// <summary>
/// Resolves case-sensitive logical identifiers against canonical read-only
/// roots. Mounts are tried from the most-specific prefix to the primary root.
/// </summary>
public sealed class AssetResolver
{
    private readonly IReadOnlyList<AssetRootBinding> _mountBindings;
    private readonly AssetRootBinding _primaryBinding;

    public AssetResolver(string rootDirectory, IEnumerable<AssetMount>? mounts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = CanonicalPathResolver.CanonicalizeRoot(rootDirectory);
        _primaryBinding = new AssetRootBinding(null, RootDirectory, AssetOrigin.PrimaryRoot, 0);

        var mountBindings = new List<AssetRootBinding>();
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mount in mounts ?? Array.Empty<AssetMount>())
        {
            ArgumentNullException.ThrowIfNull(mount);
            if (!prefixes.Add(mount.Prefix.Value))
            {
                throw new ArgumentException(
                    $"The asset mount prefix '{mount.Prefix}' is configured more than once.",
                    nameof(mounts));
            }

            var canonicalRoot = CanonicalPathResolver.CanonicalizeRoot(mount.RootDirectory);
            mountBindings.Add(new AssetRootBinding(
                mount.Prefix,
                canonicalRoot,
                AssetOrigin.Mount,
                GetComponentCount(mount.Prefix.Value)));
        }

        _mountBindings = mountBindings
            .OrderByDescending(static binding => binding.Specificity)
            .ThenBy(static binding => binding.Prefix?.Value, StringComparer.Ordinal)
            .ToArray();
        Mounts = _mountBindings
            .Select(static binding => new AssetMount(binding.Prefix!.Value, binding.CanonicalRoot))
            .ToArray();
    }

    public string RootDirectory { get; }

    public IReadOnlyList<AssetMount> Mounts { get; }

    public AssetResolution Resolve(string? logicalPath)
    {
        if (!AssetId.TryCreate(logicalPath, out var assetId, out var error))
        {
            return AssetResolution.Failure(
                null,
                new AssetDiagnostic(
                    AssetDiagnosticIds.InvalidLogicalPath,
                    AssetDiagnosticSeverity.Error,
                    $"Invalid logical asset path ({error}).",
                    logicalPath));
        }

        return Resolve(assetId);
    }

    public AssetResolution Resolve(AssetId assetId)
    {
        if (!assetId.IsValid)
        {
            return AssetResolution.Failure(
                null,
                new AssetDiagnostic(
                    AssetDiagnosticIds.InvalidLogicalPath,
                    AssetDiagnosticSeverity.Error,
                    "The logical asset identifier is uninitialized."));
        }

        AssetResolution? missing = null;
        foreach (var binding in GetResolutionBindings(assetId))
        {
            var relativePath = GetRelativePath(assetId, binding.Prefix);
            var resolution = ResolveAgainstBinding(assetId, relativePath, binding);
            if (resolution.IsSuccess)
            {
                return resolution;
            }

            if (resolution.Diagnostic?.Id != AssetDiagnosticIds.MissingAsset)
            {
                return resolution;
            }

            missing = resolution;
        }

        var mismatchedMount = _mountBindings.FirstOrDefault(binding =>
            HasPrefix(assetId.Value, binding.Prefix!.Value.Value, StringComparison.OrdinalIgnoreCase) &&
            !HasPrefix(assetId.Value, binding.Prefix.Value.Value, StringComparison.Ordinal));
        if (mismatchedMount is not null)
        {
            var mismatchedPrefix = mismatchedMount.Prefix!.Value.Value;
            var suffix = assetId.Value[mismatchedPrefix.Length..];
            var suggestion = mismatchedPrefix + suffix;
            return AssetResolution.Failure(
                assetId,
                new AssetDiagnostic(
                    AssetDiagnosticIds.CaseMismatch,
                    AssetDiagnosticSeverity.Error,
                    $"Asset mount prefix casing differs; use '{suggestion}'.",
                    assetId.Value,
                    suggestion));
        }

        return missing ?? Missing(assetId);
    }

    public AssetOpenResult OpenRead(string? logicalPath)
    {
        var resolution = Resolve(logicalPath);
        if (!resolution.IsSuccess)
        {
            return new AssetOpenResult(null, resolution);
        }

        try
        {
            var stream = new FileStream(
                resolution.FullPath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            return new AssetOpenResult(stream, resolution);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AssetOpenResult(
                null,
                AssetResolution.Failure(
                    resolution.AssetId,
                    new AssetDiagnostic(
                        AssetDiagnosticIds.IoFailure,
                        AssetDiagnosticSeverity.Error,
                        $"Asset could not be opened: {exception.Message}",
                        resolution.AssetId?.Value),
                    resolution.FullPath,
                    resolution.Origin,
                    resolution.MountPrefix));
        }
    }

    internal IReadOnlyList<AssetRootBinding> GetScanBindings() =>
        new[] { _primaryBinding }
            .Concat(_mountBindings.OrderBy(static binding => binding.Specificity))
            .ToArray();

    private static AssetResolution ResolveAgainstBinding(
        AssetId assetId,
        string relativePath,
        AssetRootBinding binding)
    {
        string canonicalCandidate;
        try
        {
            canonicalCandidate = CanonicalPathResolver.ResolveUnderRoot(
                binding.CanonicalRoot,
                relativePath);
        }
        catch (AssetContainmentException exception)
        {
            return AssetResolution.Failure(
                assetId,
                new AssetDiagnostic(
                    AssetDiagnosticIds.ContainmentViolation,
                    AssetDiagnosticSeverity.Error,
                    "The asset resolves outside its allowed canonical content root.",
                    assetId.Value),
                exception.ResolvedPath,
                binding.Origin,
                binding.Prefix?.Value);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return AssetResolution.Failure(
                assetId,
                new AssetDiagnostic(
                    AssetDiagnosticIds.IoFailure,
                    AssetDiagnosticSeverity.Error,
                    $"Asset path could not be resolved safely: {exception.Message}",
                    assetId.Value),
                origin: binding.Origin,
                mountPrefix: binding.Prefix?.Value);
        }

        ExactCaseResult casing;
        try
        {
            casing = ExactCasePathChecker.Check(binding.CanonicalRoot, relativePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AssetResolution.Failure(
                assetId,
                new AssetDiagnostic(
                    AssetDiagnosticIds.IoFailure,
                    AssetDiagnosticSeverity.Error,
                    $"Asset path casing could not be checked: {exception.Message}",
                    assetId.Value),
                canonicalCandidate,
                binding.Origin,
                binding.Prefix?.Value);
        }

        if (casing.Status == ExactCaseStatus.Mismatch)
        {
            var suggestion = CombineLogical(binding.Prefix, casing.SuggestedRelativePath!);
            return AssetResolution.Failure(
                assetId,
                new AssetDiagnostic(
                    AssetDiagnosticIds.CaseMismatch,
                    AssetDiagnosticSeverity.Error,
                    $"Asset path casing differs; use '{suggestion}'.",
                    assetId.Value,
                    suggestion),
                canonicalCandidate,
                binding.Origin,
                binding.Prefix?.Value);
        }

        if (casing.Status == ExactCaseStatus.Missing || !File.Exists(canonicalCandidate))
        {
            return Missing(assetId, canonicalCandidate, binding);
        }

        return AssetResolution.Success(
            assetId,
            canonicalCandidate,
            binding.Origin,
            binding.Prefix?.Value);
    }

    private IEnumerable<AssetRootBinding> GetResolutionBindings(AssetId assetId)
    {
        foreach (var binding in _mountBindings)
        {
            if (HasPrefix(assetId.Value, binding.Prefix!.Value.Value, StringComparison.Ordinal))
            {
                yield return binding;
            }
        }

        yield return _primaryBinding;
    }

    private static string GetRelativePath(AssetId assetId, AssetId? prefix)
    {
        if (prefix is null)
        {
            return assetId.Value;
        }

        if (assetId.Value.Length == prefix.Value.Value.Length)
        {
            return string.Empty;
        }

        return assetId.Value[(prefix.Value.Value.Length + 1)..];
    }

    private static bool HasPrefix(string assetId, string prefix, StringComparison comparison) =>
        string.Equals(assetId, prefix, comparison) ||
        (assetId.Length > prefix.Length &&
         assetId[prefix.Length] == '/' &&
         assetId.StartsWith(prefix, comparison));

    private static string CombineLogical(AssetId? prefix, string relativePath) =>
        prefix is null || relativePath.Length == 0
            ? prefix?.Value ?? relativePath
            : prefix.Value.Value + "/" + relativePath;

    private static int GetComponentCount(string value) => value.Count(static character => character == '/') + 1;

    private static AssetResolution Missing(
        AssetId assetId,
        string? fullPath = null,
        AssetRootBinding? binding = null) =>
        AssetResolution.Failure(
            assetId,
            new AssetDiagnostic(
                AssetDiagnosticIds.MissingAsset,
                AssetDiagnosticSeverity.Error,
                $"Asset '{assetId}' was not found as a regular file.",
                assetId.Value),
            fullPath,
            binding?.Origin,
            binding?.Prefix?.Value);
}

internal sealed record AssetRootBinding(
    AssetId? Prefix,
    string CanonicalRoot,
    AssetOrigin Origin,
    int Specificity);
