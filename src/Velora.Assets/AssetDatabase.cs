using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Velora.Assets.Diagnostics;
using Velora.Assets.Paths;

namespace Velora.Assets;

/// <summary>A deterministic, case-sensitive snapshot of effective project assets.</summary>
public sealed class AssetDatabase
{
    private readonly IReadOnlyDictionary<string, AssetEntry> _entriesById;

    private AssetDatabase(
        IReadOnlyDictionary<string, AssetEntry> entriesById,
        IReadOnlyList<AssetDiagnostic> diagnostics)
    {
        _entriesById = entriesById;
        Entries = entriesById.Values.OrderBy(static entry => entry.Id).ToArray();
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<AssetEntry> Entries { get; }

    public IReadOnlyList<AssetDiagnostic> Diagnostics { get; }

    public static AssetDatabase Scan(AssetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        var entries = new Dictionary<string, AssetEntry>(StringComparer.Ordinal);
        var diagnostics = new List<AssetDiagnostic>();
        foreach (var binding in resolver.GetScanBindings())
        {
            ScanBinding(binding, entries, diagnostics);
        }

        AddCaseCollisionDiagnostics(entries.Values, diagnostics);
        AddTargetAliasDiagnostics(entries.Values, diagnostics);

        return new AssetDatabase(
            new ReadOnlyDictionary<string, AssetEntry>(entries),
            diagnostics.ToArray());
    }

    public bool TryGet(AssetId assetId, [NotNullWhen(true)] out AssetEntry? entry)
    {
        if (!assetId.IsValid)
        {
            entry = null;
            return false;
        }

        return _entriesById.TryGetValue(assetId.Value, out entry);
    }

    public AssetLookupResult Lookup(string? logicalPath)
    {
        if (!AssetId.TryCreate(logicalPath, out var assetId, out var error))
        {
            return new AssetLookupResult(
                null,
                new AssetDiagnostic(
                    AssetDiagnosticIds.InvalidLogicalPath,
                    AssetDiagnosticSeverity.Error,
                    $"Invalid logical asset path ({error}).",
                    logicalPath));
        }

        if (_entriesById.TryGetValue(assetId.Value, out var exact))
        {
            return new AssetLookupResult(exact, null);
        }

        var caseMatch = Entries.FirstOrDefault(entry =>
            string.Equals(entry.Id.Value, assetId.Value, StringComparison.OrdinalIgnoreCase));
        if (caseMatch is not null)
        {
            return new AssetLookupResult(
                null,
                new AssetDiagnostic(
                    AssetDiagnosticIds.CaseMismatch,
                    AssetDiagnosticSeverity.Error,
                    $"Asset path casing differs; use '{caseMatch.Id}'.",
                    assetId.Value,
                    caseMatch.Id.Value));
        }

        return new AssetLookupResult(
            null,
            new AssetDiagnostic(
                AssetDiagnosticIds.MissingAsset,
                AssetDiagnosticSeverity.Error,
                $"Asset '{assetId}' was not found.",
                assetId.Value));
    }

    private static void ScanBinding(
        AssetRootBinding binding,
        IDictionary<string, AssetEntry> entries,
        List<AssetDiagnostic> diagnostics)
    {
        var directories = new Stack<ScanDirectory>();
        directories.Push(new ScanDirectory(binding.CanonicalRoot, string.Empty));

        while (directories.TryPop(out var directory))
        {
            string[] children;
            try
            {
                children = Directory
                    .EnumerateFileSystemEntries(directory.FullPath)
                    .OrderByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new AssetDiagnostic(
                    AssetDiagnosticIds.IoFailure,
                    AssetDiagnosticSeverity.Warning,
                    $"Asset directory could not be scanned: {exception.Message}",
                    CombineLogical(binding.Prefix, directory.RelativePath)));
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                var relativePath = directory.RelativePath.Length == 0
                    ? name
                    : directory.RelativePath + "/" + name;
                var logicalPath = CombineLogical(binding.Prefix, relativePath);

                string? linkTarget;
                try
                {
                    linkTarget = CanonicalPathResolver.GetLinkTarget(child);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new AssetDiagnostic(
                        AssetDiagnosticIds.IoFailure,
                        AssetDiagnosticSeverity.Warning,
                        $"Asset link metadata could not be read: {exception.Message}",
                        logicalPath));
                    continue;
                }

                if (Directory.Exists(child))
                {
                    if (linkTarget is not null)
                    {
                        AddDirectoryLinkDiagnostic(binding, relativePath, logicalPath, diagnostics);
                        continue;
                    }

                    directories.Push(new ScanDirectory(child, relativePath));
                    continue;
                }

                if (!File.Exists(child) && linkTarget is null)
                {
                    continue;
                }

                AddFile(binding, relativePath, logicalPath, linkTarget is not null, entries, diagnostics);
            }
        }
    }

    private static void AddDirectoryLinkDiagnostic(
        AssetRootBinding binding,
        string relativePath,
        string logicalPath,
        List<AssetDiagnostic> diagnostics)
    {
        try
        {
            _ = CanonicalPathResolver.ResolveUnderRoot(binding.CanonicalRoot, relativePath);
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.DirectoryAliasSkipped,
                AssetDiagnosticSeverity.Warning,
                "Directory symbolic-link aliases are not recursively scanned.",
                logicalPath));
        }
        catch (AssetContainmentException)
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.ContainmentViolation,
                AssetDiagnosticSeverity.Error,
                "Directory symbolic link escapes its allowed content root.",
                logicalPath));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.IoFailure,
                AssetDiagnosticSeverity.Warning,
                $"Directory symbolic link could not be resolved: {exception.Message}",
                logicalPath));
        }
    }

    private static void AddFile(
        AssetRootBinding binding,
        string relativePath,
        string logicalPath,
        bool isSymbolicLink,
        IDictionary<string, AssetEntry> entries,
        List<AssetDiagnostic> diagnostics)
    {
        if (!AssetId.TryCreate(logicalPath, out var assetId, out var pathError))
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.InvalidLogicalPath,
                AssetDiagnosticSeverity.Error,
                $"Filesystem entry cannot be represented as a portable asset ID ({pathError}).",
                logicalPath));
            return;
        }

        string canonicalPath;
        try
        {
            canonicalPath = CanonicalPathResolver.ResolveUnderRoot(binding.CanonicalRoot, relativePath);
        }
        catch (AssetContainmentException)
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.ContainmentViolation,
                AssetDiagnosticSeverity.Error,
                "Asset symbolic link escapes its allowed content root.",
                logicalPath));
            return;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.IoFailure,
                AssetDiagnosticSeverity.Warning,
                $"Asset could not be resolved while scanning: {exception.Message}",
                logicalPath));
            return;
        }

        if (!File.Exists(canonicalPath))
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.BrokenLink,
                AssetDiagnosticSeverity.Warning,
                "Asset symbolic link target does not exist.",
                logicalPath));
            return;
        }

        long length;
        try
        {
            length = new FileInfo(canonicalPath).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.IoFailure,
                AssetDiagnosticSeverity.Warning,
                $"Asset metadata could not be read: {exception.Message}",
                logicalPath));
            return;
        }

        var entry = new AssetEntry(
            assetId,
            canonicalPath,
            length,
            binding.Origin,
            binding.Prefix?.Value,
            isSymbolicLink);
        if (entries.TryGetValue(assetId.Value, out var previous))
        {
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.DuplicateLogicalId,
                AssetDiagnosticSeverity.Warning,
                $"Asset '{assetId}' from {entry.Origin} shadows the same ID from {previous.Origin}.",
                assetId.Value));
        }

        entries[assetId.Value] = entry;
    }

    private static void AddCaseCollisionDiagnostics(
        IEnumerable<AssetEntry> entries,
        List<AssetDiagnostic> diagnostics)
    {
        foreach (var group in entries
                     .GroupBy(static entry => entry.Id.Value, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            var ids = group.Select(static entry => entry.Id.Value).Order(StringComparer.Ordinal).ToArray();
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.CaseCollision,
                AssetDiagnosticSeverity.Error,
                $"Asset IDs differ only by case: {string.Join(", ", ids)}.",
                ids[0]));
        }
    }

    private static void AddTargetAliasDiagnostics(
        IEnumerable<AssetEntry> entries,
        List<AssetDiagnostic> diagnostics)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        foreach (var group in entries
                     .GroupBy(static entry => entry.FullPath, comparer)
                     .Where(static group => group.Count() > 1))
        {
            var ids = group.Select(static entry => entry.Id.Value).Order(StringComparer.Ordinal).ToArray();
            diagnostics.Add(new AssetDiagnostic(
                AssetDiagnosticIds.DuplicateTargetAlias,
                AssetDiagnosticSeverity.Warning,
                $"Multiple asset IDs resolve to the same canonical file: {string.Join(", ", ids)}.",
                ids[0]));
        }
    }

    private static string CombineLogical(AssetId? prefix, string relativePath) =>
        prefix is null || relativePath.Length == 0
            ? prefix?.Value ?? relativePath
            : prefix.Value.Value + "/" + relativePath;

    private sealed record ScanDirectory(string FullPath, string RelativePath);
}
