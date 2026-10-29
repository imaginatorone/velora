namespace Velora.Platform.Desktop;

internal sealed class ContainedFileStore
{
    private const string AtomicTemporaryPrefix = ".velora-atomic-";

    private readonly CanonicalStoragePathResolver _paths;
    private readonly int _maximumFileBytes;

    internal ContainedFileStore(string rootDirectory, int maximumFileBytes)
    {
        if (maximumFileBytes is < 1 or > DesktopStorageLimits.MaximumFileBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFileBytes),
                maximumFileBytes,
                $"The file limit must range from 1 through {DesktopStorageLimits.MaximumFileBytes} bytes.");
        }

        _paths = new CanonicalStoragePathResolver(rootDirectory);
        _maximumFileBytes = maximumFileBytes;
    }

    internal bool Exists(StoragePath path)
    {
        ResolvedStoragePath resolved = Resolve(path);
        return File.Exists(resolved.CanonicalPath);
    }

    internal bool TryRead(StoragePath path, out ReadOnlyMemory<byte> data)
    {
        ResolvedStoragePath resolved = Resolve(path);
        try
        {
            using var input = new FileStream(
                resolved.CanonicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);

            long length = input.Length;
            if (length > _maximumFileBytes)
            {
                throw new InvalidDataException(
                    $"The stored file exceeds the {_maximumFileBytes}-byte host limit.");
            }

            byte[] bytes = GC.AllocateUninitializedArray<byte>((int)length);
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    $"The stored file exceeds the {_maximumFileBytes}-byte host limit.");
            }

            data = bytes;
            return true;
        }
        catch (FileNotFoundException)
        {
            data = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            data = default;
            return false;
        }
    }

    internal void WriteAtomic(StoragePath path, ReadOnlySpan<byte> data)
    {
        if (data.Length > _maximumFileBytes)
        {
            throw new InvalidDataException(
                $"The stored file exceeds the {_maximumFileBytes}-byte host limit.");
        }

        ResolvedStoragePath resolved = Resolve(path);
        if (Directory.Exists(resolved.CanonicalPath))
        {
            throw new IOException("A directory already exists at the requested storage path.");
        }

        string? parent = Path.GetDirectoryName(resolved.CanonicalPath);
        if (parent is null)
        {
            throw new IOException("The storage path has no parent directory.");
        }

        Directory.CreateDirectory(parent);
        parent = _paths.ResolveHostPath(parent);
        string destination = _paths.Resolve(path).CanonicalPath;
        if (Directory.Exists(destination))
        {
            throw new IOException("A directory already exists at the requested storage path.");
        }

        string temporaryPath = CreateTemporaryPath(parent);
        try
        {
            using (var output = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 64 * 1024,
                       FileOptions.WriteThrough))
            {
                _ = _paths.ResolveHostPath(temporaryPath);
                output.Write(data);
                output.Flush(flushToDisk: true);
            }

            destination = _paths.Resolve(path).CanonicalPath;
            _ = _paths.ResolveHostPath(temporaryPath);
            File.Move(temporaryPath, destination, overwrite: true);
            temporaryPath = string.Empty;
        }
        finally
        {
            if (temporaryPath.Length != 0)
            {
                TryDeleteTemporary(temporaryPath);
            }
        }
    }

    internal bool Delete(StoragePath path)
    {
        ResolvedStoragePath resolved = Resolve(path);
        if (!File.Exists(resolved.CanonicalPath))
        {
            return false;
        }

        File.Delete(resolved.CanonicalPath);
        return true;
    }

    internal IReadOnlyList<StorageEntry> List()
    {
        var entries = new List<StorageEntry>();
        var activeDirectories = new HashSet<string>(GetPathComparer());
        EnumerateDirectory(_paths.CanonicalRoot, string.Empty, activeDirectories, entries);
        return entries
            .OrderBy(static entry => entry.Path.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private void EnumerateDirectory(
        string directoryPath,
        string relativeDirectory,
        HashSet<string> activeDirectories,
        List<StorageEntry> entries)
    {
        string canonicalDirectory;
        try
        {
            canonicalDirectory = _paths.ResolveHostPath(directoryPath);
        }
        catch (DesktopStorageContainmentException)
        {
            return;
        }

        if (!activeDirectories.Add(canonicalDirectory))
        {
            return;
        }

        try
        {
            FileSystemInfo[] children = new DirectoryInfo(directoryPath)
                .EnumerateFileSystemInfos()
                .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
                .ToArray();

            foreach (FileSystemInfo child in children)
            {
                if (child.Name.StartsWith(AtomicTemporaryPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string relativePath = relativeDirectory.Length == 0
                    ? child.Name
                    : $"{relativeDirectory}/{child.Name}";
                if (!StoragePath.TryParse(relativePath, out StoragePath storagePath))
                {
                    continue;
                }

                ResolvedStoragePath resolved;
                try
                {
                    resolved = _paths.Resolve(storagePath);
                }
                catch (DesktopStorageContainmentException)
                {
                    continue;
                }

                if (Directory.Exists(resolved.CanonicalPath))
                {
                    EnumerateDirectory(child.FullName, relativePath, activeDirectories, entries);
                    continue;
                }

                if (!File.Exists(resolved.CanonicalPath))
                {
                    continue;
                }

                var file = new FileInfo(resolved.CanonicalPath);
                entries.Add(new StorageEntry(
                    storagePath,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc)));
            }
        }
        finally
        {
            _ = activeDirectories.Remove(canonicalDirectory);
        }
    }

    private ResolvedStoragePath Resolve(StoragePath path)
    {
        if (path.IsValid
            && path.Value.Split('/').Any(static segment =>
                segment.StartsWith(AtomicTemporaryPrefix, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The storage path uses a reserved host filename prefix.", nameof(path));
        }

        return _paths.Resolve(path);
    }

    private static string CreateTemporaryPath(string parent)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            string candidate = Path.Combine(parent, $"{AtomicTemporaryPrefix}{Guid.NewGuid():N}.tmp");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to allocate a unique atomic-write temporary file.");
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Preserve the original write failure. Internal temporary files are
            // hidden from logical enumeration and can be cleaned by the host.
        }
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
