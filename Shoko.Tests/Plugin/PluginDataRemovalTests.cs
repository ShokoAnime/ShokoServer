using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Covers removing a plugin's data when it is uninstalled: off unless asked for, left for the
/// next start while the plugin still runs, and kept while another installed version uses it.
/// </summary>
public sealed class PluginDataRemovalTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), $"shoko-plugin-data-removal-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void UninstallingWithTheDataRemovesItsDatabasesCacheAndConfigurationOnTheNextStart()
    {
        var (manager, plugins) = CreateManager();
        var plugin = Add(plugins, Guid.NewGuid());
        var other = Guid.NewGuid();
        CreateData(plugin.ID);
        CreateData(other);

        manager.UninstallPlugin(plugin, purgeData: true);

        Assert.False(Directory.Exists(Folder("configuration", plugin.ID)));
        Assert.True(File.Exists(Path.Join(Folder("data", plugin.ID), "notes.db3")));

        manager.RemovePluginDataMarkedForRemoval();

        Assert.False(Directory.Exists(Folder("data", plugin.ID)));
        Assert.False(Directory.Exists(Folder("cache", plugin.ID)));
        Assert.True(Directory.Exists(Folder("data", other)));
        Assert.True(Directory.Exists(Folder("cache", other)));
    }

    [Fact]
    public void UninstallingWithoutTheDataKeepsIt()
    {
        var (manager, plugins) = CreateManager();
        var plugin = Add(plugins, Guid.NewGuid());
        CreateData(plugin.ID);

        manager.UninstallPlugin(plugin, purgeConfiguration: true);
        manager.RemovePluginDataMarkedForRemoval();

        Assert.False(Directory.Exists(Folder("configuration", plugin.ID)));
        Assert.True(Directory.Exists(Folder("data", plugin.ID)));
        Assert.True(Directory.Exists(Folder("cache", plugin.ID)));
    }

    [Fact]
    public void TheDataIsKeptWhileAnotherInstalledVersionUsesIt()
    {
        var (manager, plugins) = CreateManager();
        var id = Guid.NewGuid();
        var plugin = Add(plugins, id, "Old.dll");
        Add(plugins, id, "New.dll");
        CreateData(id);

        manager.UninstallPlugin(plugin, purgeData: true);
        manager.RemovePluginDataMarkedForRemoval();

        Assert.True(Directory.Exists(Folder("data", id)));
    }

    private string Folder(string kind, Guid pluginID)
        => Path.Join(_root, kind, pluginID.ToString());

    private void CreateData(Guid pluginID)
    {
        foreach (var kind in new[] { "configuration", "data", "cache" })
            Directory.CreateDirectory(Folder(kind, pluginID));
        File.WriteAllText(Path.Join(Folder("data", pluginID), "notes.db3"), string.Empty);
        File.WriteAllText(Path.Join(Folder("cache", pluginID), "cached.bin"), string.Empty);
    }

    private LocalPluginInfo Add(List<LocalPluginInfo> plugins, Guid id, string dllName = "SomePlugin.dll")
    {
        var plugin = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), id, Path.Join(_root, "plugins", dllName));
        plugins.Add(plugin);
        return plugin;
    }

    private (PluginManager Manager, List<LocalPluginInfo> Plugins) CreateManager()
    {
        var paths = Mock.Of<IApplicationPaths>(applicationPaths =>
            applicationPaths.ConfigurationsPath == Path.Join(_root, "configuration") &&
            applicationPaths.DatabasePath == Path.Join(_root, "data") &&
            applicationPaths.CachePath == Path.Join(_root, "cache") &&
            applicationPaths.PluginsPath == Path.Join(_root, "plugins"));

        var manager = TestPluginManager.Create(paths);
        return (manager, TestPluginManager.Plugins(manager));
    }
}
