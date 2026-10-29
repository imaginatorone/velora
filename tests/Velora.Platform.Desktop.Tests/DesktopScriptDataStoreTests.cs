using Velora.Platform.Desktop.Tests.TestSupport;

namespace Velora.Platform.Desktop.Tests;

public sealed class DesktopScriptDataStoreTests
{
    [Fact]
    public void ProvidesOnlyContainedReadWriteDeleteOperations()
    {
        using var temporary = new TemporaryDirectory();
        var storage = new DesktopScriptDataStore(temporary.CreateDirectory("script-data"));
        StoragePath path = StoragePath.Parse("routes/chapter-1.dat");

        storage.WriteAtomic(path, "north"u8);

        Assert.True(storage.TryRead(path, out ReadOnlyMemory<byte> data));
        Assert.Equal("north"u8.ToArray(), data.ToArray());
        Assert.True(storage.Delete(path));
        Assert.False(storage.TryRead(path, out _));
        Assert.DoesNotContain(
            typeof(IScriptDataStore).GetMethods(),
            static method => method.Name.Contains("List", StringComparison.Ordinal)
                || method.ReturnType == typeof(string));
    }

    [Fact]
    public void SaveAndScriptStoresUseSeparateApplicationRoots()
    {
        using var temporary = new TemporaryDirectory();
        var environment = new DesktopHostEnvironment
        {
            Platform = DesktopPlatformKind.Linux,
            HomeDirectory = temporary.FullPath,
            XdgDataHome = temporary.CreateDirectory("xdg"),
        };
        DesktopStorageServices services = DesktopStorageServices.Create(
            "com.velora.test-game",
            environment,
            maximumFileBytes: 1024);
        StoragePath sameLogicalName = StoragePath.Parse("state.dat");

        services.Saves.WriteAtomic(sameLogicalName, "save"u8);
        services.ScriptData.WriteAtomic(sameLogicalName, "script"u8);

        Assert.True(services.Saves.TryRead(sameLogicalName, out ReadOnlyMemory<byte> saveData));
        Assert.True(services.ScriptData.TryRead(sameLogicalName, out ReadOnlyMemory<byte> scriptData));
        Assert.Equal("save"u8.ToArray(), saveData.ToArray());
        Assert.Equal("script"u8.ToArray(), scriptData.ToArray());
    }

    [Fact]
    public void RejectsSymlinkEscapeForMissingWriteTarget()
    {
        using var temporary = new TemporaryDirectory();
        string root = temporary.CreateDirectory("script-data");
        string outside = temporary.CreateDirectory("outside");
        bool linkCreated = temporary.TryCreateDirectoryLink("script-data/escape", outside);
        Assert.True(linkCreated || OperatingSystem.IsWindows(), "Symbolic links must be available on Unix test hosts.");
        if (!linkCreated)
        {
            return;
        }

        var storage = new DesktopScriptDataStore(root);

        Assert.Throws<DesktopStorageContainmentException>(() =>
            storage.WriteAtomic(StoragePath.Parse("escape/new.dat"), "blocked"u8));
        Assert.False(File.Exists(temporary.GetPath("outside/new.dat")));
    }

    [Fact]
    public void DesktopAdapterHasNoUiOrNativeBackendDependency()
    {
        string[] references = typeof(DesktopScriptDataStore).Assembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, static name =>
            name.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SDL", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Rendering", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Velora.Platform.Abstractions", references);
    }
}
