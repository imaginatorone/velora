namespace Velora.Assets.Paths;

internal static class CanonicalPathResolver
{
    private const int MaximumLinkRedirects = 64;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string CanonicalizeRoot(string rootDirectory)
    {
        var absolute = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(absolute))
        {
            throw new DirectoryNotFoundException($"Asset root does not exist: {absolute}");
        }

        var canonical = ResolveLinks(absolute);
        if (!Directory.Exists(canonical))
        {
            throw new DirectoryNotFoundException($"Canonical asset root does not exist: {canonical}");
        }

        return Path.TrimEndingDirectorySeparator(canonical);
    }

    public static string ResolveUnderRoot(string canonicalRoot, string portableRelativePath)
    {
        var platformRelativePath = portableRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(canonicalRoot, platformRelativePath));
        var canonicalCandidate = ResolveLinks(candidate);
        if (!Contains(canonicalRoot, canonicalCandidate))
        {
            throw new AssetContainmentException(canonicalCandidate);
        }

        return canonicalCandidate;
    }

    public static string ResolveLinks(string path)
    {
        var candidate = Path.GetFullPath(path);
        var redirectedPaths = new HashSet<string>(PathComparer);

        for (var redirectCount = 0; redirectCount < MaximumLinkRedirects; redirectCount++)
        {
            if (!redirectedPaths.Add(candidate))
            {
                throw new IOException($"A symbolic-link cycle was detected while resolving '{path}'.");
            }

            var root = Path.GetPathRoot(candidate);
            if (string.IsNullOrEmpty(root))
            {
                throw new IOException($"Path has no filesystem root: {candidate}");
            }

            var relative = candidate[root.Length..];
            var components = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var redirected = false;

            for (var index = 0; index < components.Length; index++)
            {
                current = Path.Combine(current, components[index]);
                var linkTarget = GetLinkTarget(current);
                if (linkTarget is null)
                {
                    if (!File.Exists(current) && !Directory.Exists(current))
                    {
                        for (var remaining = index + 1; remaining < components.Length; remaining++)
                        {
                            current = Path.Combine(current, components[remaining]);
                        }

                        return Path.GetFullPath(current);
                    }

                    continue;
                }

                var parent = Path.GetDirectoryName(current) ?? root;
                var target = Path.IsPathRooted(linkTarget)
                    ? linkTarget
                    : Path.Combine(parent, linkTarget);
                for (var remaining = index + 1; remaining < components.Length; remaining++)
                {
                    target = Path.Combine(target, components[remaining]);
                }

                candidate = Path.GetFullPath(target);
                redirected = true;
                break;
            }

            if (!redirected)
            {
                return Path.GetFullPath(current);
            }
        }

        throw new IOException($"Too many symbolic-link redirects while resolving '{path}'.");
    }

    public static string? GetLinkTarget(string path)
    {
        try
        {
            var target = new FileInfo(path).LinkTarget;
            if (target is not null)
            {
                return target;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            return new DirectoryInfo(path).LinkTarget;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public static bool Contains(string canonicalRoot, string canonicalCandidate)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(canonicalRoot));
        var candidate = Path.GetFullPath(canonicalCandidate);
        if (string.Equals(root, candidate, PathComparison))
        {
            return true;
        }

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, PathComparison);
    }
}

internal sealed class AssetContainmentException(string resolvedPath) : IOException
{
    public string ResolvedPath { get; } = resolvedPath;
}
