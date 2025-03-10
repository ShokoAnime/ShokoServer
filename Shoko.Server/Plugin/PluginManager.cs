using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Plugin;

#nullable enable
namespace Shoko.Server.Plugin;

public partial class PluginManager() : IPluginManager
{
    private Dictionary<Guid, PluginInfo>? _pluginInfos = null;

    public void AddParts(IEnumerable<IPlugin> plugins)
    {
        if (_pluginInfos is not null)
            return;

        _pluginInfos = plugins
            .Select(plugin =>
            {
                var pluginType = plugin.GetType();
                return new PluginInfo()
                {
                    ID = plugin.ID,
                    Description = pluginType.GetDescription(),
                    Version = pluginType.Assembly.GetName().Version ?? new(0, 0, 0),
                    Plugin = plugin,
                    PluginType = pluginType,
                    CanUninstall = pluginType != typeof(CorePlugin),
                };
            })
            .OrderByDescending(p => p.PluginType == typeof(CorePlugin))
            .ThenBy(p => p.Name)
            .ThenBy(p => p.Version)
            .ToDictionary(p => p.ID);
    }

    private Dictionary<Guid, PluginInfo> EnsurePluginInfos()
    {
        if (_pluginInfos is null)
            throw new InvalidOperationException("Plugins have not been initialized yet.");

        return _pluginInfos;
    }

    public IEnumerable<PluginInfo> GetPluginInfos()
        => EnsurePluginInfos().Values;

    public PluginInfo? GetPluginInfo(Guid pluginId)
        => EnsurePluginInfos().TryGetValue(pluginId, out var pluginInfo) ? pluginInfo : null;

    public PluginInfo? GetPluginInfo(IPlugin plugin)
        => GetPluginInfo(plugin.GetType());

    public PluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin
        => GetPluginInfo(typeof(TPlugin));

    public PluginInfo? GetPluginInfo(Type type)
        => EnsurePluginInfos().Values.FirstOrDefault(p => p.PluginType == type);
}
