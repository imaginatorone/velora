using System.Text;

namespace Velora.Assets.Tests.TestSupport;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        FullPath = Path.Combine(
            Path.GetTempPath(),
            $"velora-assets-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FullPath);
    }

    public string FullPath { get; }

    public string CreateDirectory(string relativePath)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteText(string relativePath, string contents)
    {
        var path = GetPath(relativePath);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string GetPath(string relativePath) => Path.Combine(
        FullPath,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool TryCreateDirectoryLink(string relativeLinkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(GetPath(relativeLinkPath), targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public bool TryCreateFileLink(string relativeLinkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(GetPath(relativeLinkPath), targetPath);
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
        if (Directory.Exists(FullPath))
        {
            Directory.Delete(FullPath, recursive: true);
        }
    }
}
