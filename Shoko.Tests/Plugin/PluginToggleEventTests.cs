using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   <see cref="PluginManager.PluginEnabled"/> and <see cref="PluginManager.PluginDisabled"/> are
///   raised by the toggles only when the plugin's state changed.
/// </summary>
public sealed class PluginToggleEventTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), "shoko-toggle-" + Guid.NewGuid().ToString("N"));

    public PluginToggleEventTests()
        => Directory.CreateDirectory(_directory);

    public void Dispose()
        => Directory.Delete(_directory, true);

    [Fact]
    public async Task Disable_RaisesDisabledOnce()
    {
        var (manager, events) = CreateManager();
        var plugin = CreatePlugin();

        manager.DisablePlugin(plugin);
        var (kind, args) = await events.NextAsync();

        Assert.Equal("disabled", kind);
        Assert.Same(plugin, args.Plugin);
        Assert.False(args.Plugin.IsEnabled);

        // A second disable changes nothing, so the next event is the enable after it.
        manager.DisablePlugin(plugin);
        manager.EnablePlugin(plugin);
        Assert.Equal("enabled", (await events.NextAsync()).Kind);
    }

    [Fact]
    public async Task Enable_RaisesEnabledOnlyWhenItWasDisabled()
    {
        var (manager, events) = CreateManager();
        var plugin = CreatePlugin();

        // Enabled already, so the next event is the disable after it.
        manager.EnablePlugin(plugin);
        manager.DisablePlugin(plugin);
        Assert.Equal("disabled", (await events.NextAsync()).Kind);

        manager.EnablePlugin(plugin);
        var (kind, args) = await events.NextAsync();
        Assert.Equal("enabled", kind);
        Assert.True(args.Plugin.IsEnabled);
    }

    private Abstractions.Plugin.Models.LocalPluginInfo CreatePlugin()
        => PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), _directory, Path.Join(_directory, "SomePlugin.dll"));

    private static (PluginManager Manager, EventLog Events) CreateManager()
    {
        var manager = TestPluginManager.Create();
        var events = new EventLog();
        manager.PluginEnabled += (_, e) => events.Add("enabled", e);
        manager.PluginDisabled += (_, e) => events.Add("disabled", e);
        return (manager, events);
    }

    /// <summary>
    ///   The toggle events as they arrive, off the thread that raised them.
    /// </summary>
    private sealed class EventLog
    {
        private readonly BlockingCollection<(string Kind, PluginToggledEventArgs Args)> _events = [];

        public void Add(string kind, PluginToggledEventArgs args)
            => _events.Add((kind, args));

        public Task<(string Kind, PluginToggledEventArgs Args)> NextAsync()
            => Task.Run(() => _events.TryTake(out var item, TimeSpan.FromSeconds(10)) ? item : throw new TimeoutException("No toggle event was raised."));
    }
}
