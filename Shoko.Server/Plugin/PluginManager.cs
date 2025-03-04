using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Namotion.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Plugin;

public partial class PluginManager() : IPluginManager
{
    private readonly object _lock = new();

    private Dictionary<Guid, PluginInfo>? _pluginInfos = null;

    private Dictionary<Guid, PluginInfo> EnsurePluginInfos()
    {
        if (_pluginInfos is not null)
            return _pluginInfos;

        lock (_lock)
        {
            if (_pluginInfos is not null)
                return _pluginInfos;

            return _pluginInfos = Loader.Plugins
                .Select(tuple =>
                {
                    var (pluginType, plugin) = tuple;
                    return new PluginInfo()
                    {
                        ID = plugin.ID,
                        Description = GetDescription(pluginType),
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

    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDisplayName(Type type)
        => GetDisplayName(type.ToContextualType());

    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDisplayName(ContextualType type)
    {
        var displayAttribute = type.GetAttribute<DisplayAttribute>(false);
        var displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name)
            ? displayAttribute.Name
            : DisplayNameRegex().Replace(type.Name, " $1");

        return displayName;
    }

    public static string GetDisplayName(string name)
        => DisplayNameRegex().Replace(name, " $1");

    /// <summary>
    /// Simple regex to auto-infer display name from PascalCase class names.
    /// </summary>
    [GeneratedRegex(@"(\B[A-Z](?![A-Z]))")]
    private static partial Regex DisplayNameRegex();

    /// <summary>
    /// Gets the description for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDescription(Type type)
        => GetDescription(type.ToContextualType());

    /// <summary>
    /// Gets the description for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDescription(ContextualType type)
    {
        var description = type.GetAttribute<DisplayAttribute>(false)?.Description;
        if (string.IsNullOrEmpty(description))
            description = type.GetXmlDocsSummary() ?? string.Empty;

        description = description
            .Replace(BreakTwoRegex(), "\0")
            .Replace(BreakRegex(), " ")
            .Replace(SpaceRegex(), " ")
            .Replace("\0", "\n")
            .Trim();

        return description;
    }

    /// <summary>
    /// Simple regex to collapse multiple lines into a single line.
    /// </summary>
    [GeneratedRegex(@"(\r\n|\r|\n){2,}")]
    private static partial Regex BreakTwoRegex();

    /// <summary>
    /// Simple regex to convert single line breaks to spaces.
    /// </summary>
    [GeneratedRegex(@"(\r\n|\r|\n)")]
    private static partial Regex BreakRegex();

    /// <summary>
    /// Simple regex to convert multiple spaces or tabs to a single space.
    /// </summary>
    [GeneratedRegex(@"[\t ]+")]
    private static partial Regex SpaceRegex();
}
