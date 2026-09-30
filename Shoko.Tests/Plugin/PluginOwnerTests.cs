using System;
using System.IO;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Covers which assemblies no plugin owns (<see cref="PluginManager.IsPassThrough"/>),
///   and how <see cref="PluginManager.FindPluginByLocation"/> tells which
///   plugin an assembly was loaded from.
/// </summary>
public class PluginOwnerTests
{
    [Fact]
    public void IsPassThrough_CoversTheRuntimeAndTheAbstractionsOnly()
    {
        Assert.True(PluginManager.IsPassThrough(typeof(object).Assembly));
        Assert.True(PluginManager.IsPassThrough(typeof(Shoko.Abstractions.Plugin.IPlugin).Assembly));
        Assert.True(PluginManager.IsPassThrough(typeof(Microsoft.Extensions.Logging.ILogger).Assembly));
        Assert.False(PluginManager.IsPassThrough(typeof(PluginManager).Assembly));
        Assert.False(PluginManager.IsPassThrough(typeof(PluginOwnerTests).Assembly));
    }

    private static readonly string _pluginsPath = Path.Combine(Path.GetTempPath(), "shoko-plugin-caller-tests", "plugins");

    [Fact]
    public void FindPluginByLocation_TakesAnyFileInAPluginsFolder()
    {
        var folder = Path.Combine(_pluginsPath, "Folder");
        var plugin = PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), folder, Path.Combine(folder, "Folder.dll"));

        Assert.Same(plugin, PluginManager.FindPluginByLocation(Path.Combine(folder, "Folder.dll"), [plugin]));
        Assert.Same(plugin, PluginManager.FindPluginByLocation(Path.Combine(folder, "lib", "Dependency.dll"), [plugin]));
    }

    [Fact]
    public void FindPluginByLocation_DoesNotTakeASiblingFolderStartingWithTheSameName()
    {
        var folder = Path.Combine(_pluginsPath, "Folder");
        var plugin = PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), folder, Path.Combine(folder, "Folder.dll"));

        Assert.Null(PluginManager.FindPluginByLocation(Path.Combine(_pluginsPath, "FolderTwo", "Folder.dll"), [plugin]));
        Assert.Null(PluginManager.FindPluginByLocation(Path.Combine(_pluginsPath, "Folder.dll"), [plugin]));
    }

    [Fact]
    public void FindPluginByLocation_TakesASingleFilePluginByItsDllOnly()
    {
        var dllPath = Path.Combine(_pluginsPath, "SingleFile.dll");
        var plugin = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), dllPath);

        Assert.Same(plugin, PluginManager.FindPluginByLocation(dllPath, [plugin]));
        Assert.Null(PluginManager.FindPluginByLocation(Path.Combine(_pluginsPath, "Other.dll"), [plugin]));
    }

    [Fact]
    public void FindPluginByLocation_GivesNothingForAnAssemblyLoadedFromMemory()
    {
        var plugin = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), Path.Combine(_pluginsPath, "SingleFile.dll"));

        Assert.Null(PluginManager.FindPluginByLocation(string.Empty, [plugin]));
    }
}
