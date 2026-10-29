namespace Velora.Platform.Desktop;

internal sealed class CanonicalStoragePathResolver
{
    private const int MaximumLinkRedirects = 64;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly string _canonicalRoot;

    internal CanonicalStoragePathResolver(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException(
                "The host storage root must be a fully qualified path.",
                nameof(rootDirectory));
        }

        string absoluteRoot = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(absoluteRoot);
        _canonicalRoot = Path.TrimEndingDirectorySeparator(ResolveLinks(absoluteRoot));
        if (!Directory.Exists(_canonicalRoot))
        {
            throw new DirectoryNotFoundException("The canonical storage root does not exist.");
        }
    }

    internal string CanonicalRoot => _canonicalRoot;

    internal ResolvedStoragePath Resolve(StoragePath path)
    {
        Validate(path);

        string platformRelativePath = path.Value.Replace('/', Path.DirectorySeparatorChar);
        string operationPath = Path.GetFullPath(Path.Combine(_canonicalRoot, platformRelativePath));
        if (!Contains(operationPath))
        {
            throw new DesktopStorageContainmentException();
        }

        string canonicalPath = ResolveLinks(operationPath);
        if (!Contains(canonicalPath))
        {
            throw new DesktopStorageContainmentException();
        }

        return new ResolvedStoragePath(operationPath, canonicalPath);
    }

    internal string ResolveHostPath(string path)
    {
        string canonicalPath = ResolveLinks(path);
        if (!Contains(canonicalPath))
        {
            throw new DesktopStorageContainmentException();
        }

        return canonicalPath;
    }

    internal static string ResolveLinks(string path)
    {
        string candidate = Path.GetFullPath(path);
        var redirectedPaths = new HashSet<string>(PathComparer);

        for (int redirectCount = 0; redirectCount < MaximumLinkRedirects; redirectCount++)
        {
            if (!redirectedPaths.Add(candidate))
            {
                throw new IOException("A symbolic-link cycle was detected in the storage path.");
            }

            string? filesystemRoot = Path.GetPathRoot(candidate);
            if (string.IsNullOrEmpty(filesystemRoot))
            {
                throw new IOException("The storage path has no filesystem root.");
            }

            string relative = candidate[filesystemRoot.Length..];
            string[] components = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            string current = filesystemRoot;
            bool redirected = false;

            for (int index = 0; index < components.Length; index++)
            {
                current = Path.Combine(current, components[index]);
                string? linkTarget = GetLinkTarget(current);
                if (linkTarget is null)
                {
                    if (!File.Exists(current) && !Directory.Exists(current))
                    {
                        for (int remaining = index + 1; remaining < components.Length; remaining++)
                        {
                            current = Path.Combine(current, components[remaining]);
                        }

                        return Path.GetFullPath(current);
                    }

                    continue;
                }

                string parent = Path.GetDirectoryName(current) ?? filesystemRoot;
                string target = Path.IsPathRooted(linkTarget)
                    ? linkTarget
                    : Path.Combine(parent, linkTarget);
                for (int remaining = index + 1; remaining < components.Length; remaining++)
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

        throw new IOException("The storage path contains too many symbolic-link redirects.");
    }

    private static string? GetLinkTarget(string path)
    {
        try
        {
            string? fileTarget = new FileInfo(path).LinkTarget;
            if (fileTarget is not null)
            {
                return fileTarget;
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

    private bool Contains(string candidate)
    {
        string absoluteCandidate = Path.GetFullPath(candidate);
        if (string.Equals(_canonicalRoot, absoluteCandidate, PathComparison))
        {
            return true;
        }

        string rootPrefix = _canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _canonicalRoot
            : _canonicalRoot + Path.DirectorySeparatorChar;
        return absoluteCandidate.StartsWith(rootPrefix, PathComparison);
    }

    private static void Validate(StoragePath path)
    {
        if (!path.IsValid)
        {
            throw new ArgumentException("A valid relative storage path is required.", nameof(path));
        }
    }
}

internal readonly record struct ResolvedStoragePath(string OperationPath, string CanonicalPath);
