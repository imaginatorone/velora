namespace Velora.Platform.Desktop.Tests.TestSupport;

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), $"velora-platform-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FullPath);
    }

    internal string FullPath { get; }

    internal string GetPath(string relativePath) => Path.Combine(
        FullPath,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    internal string CreateDirectory(string relativePath)
    {
        string path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    internal void Write(string relativePath, ReadOnlySpan<byte> data)
    {
        string path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    internal bool TryCreateDirectoryLink(string relativePath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(GetPath(relativePath), targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    internal bool TryCreateFileLink(string relativePath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(GetPath(relativePath), targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
