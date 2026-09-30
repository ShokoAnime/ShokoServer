using System;
using System.Collections.Generic;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   Doubles for the plugin registry's metadata.
/// </summary>
/// <remarks>
///   <see cref="LocalPluginInfo"/> is the answer to "which plugin does this provider belong to",
///   so anything testing how a provider service attributes its parts has to build one, and every
///   required member has to be filled in even when the question being asked is only about
///   <see cref="LocalPluginInfo.PluginType"/>.
/// </remarks>
public static class PluginTestDoubles
{
    /// <summary>
    ///   A plugin entry attributed to <paramref name="pluginType"/>, as registered by the core.
    /// </summary>
    public static LocalPluginInfo CorePluginInfo(Type pluginType, Guid id)
        => LocalPlugin(pluginType, id, "Shoko.Server.dll", canUninstall: false, name: "Shoko Core");

    /// <summary>
    ///   A plugin entry for an ordinary plugin that can be uninstalled, holding
    ///   <paramref name="plugin"/> as its instance if given.
    /// </summary>
    public static LocalPluginInfo InstalledPluginInfo(Type pluginType, Guid id, string dllName = "SomePlugin.dll", IPlugin? plugin = null)
        => LocalPlugin(pluginType, id, dllName, canUninstall: true, name: "Some Plugin", plugin);

    /// <summary>
    ///   A plugin entry for an ordinary plugin installed in a folder of its own.
    /// </summary>
    public static LocalPluginInfo InstalledPluginInfoInFolder(Type pluginType, Guid id, string containingDirectory, string dllPath)
        => LocalPlugin(pluginType, id, dllPath, canUninstall: true, name: "Some Plugin", containingDirectory: containingDirectory);

    /// <summary>
    ///   A plugin entry for an ordinary plugin with the given text to search.
    /// </summary>
    /// <param name="id">The plugin ID.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="description">The plugin description.</param>
    /// <param name="tags">The plugin tags.</param>
    /// <param name="authors">The plugin authors, if any.</param>
    /// <returns>The plugin entry.</returns>
    public static LocalPluginInfo DescribedPluginInfo(Guid id, string name, string description, IReadOnlyList<string> tags, string? authors)
        => LocalPlugin(typeof(TestPlugin), id, "SomePlugin.dll", canUninstall: true, name, description: description, tags: tags, authors: authors);

    /// <summary>
    ///   A loaded plugin entry with the given text to search, whose plugin
    ///   shows one page.
    /// </summary>
    /// <param name="id">The plugin ID.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="description">The plugin description.</param>
    /// <param name="pageName">The name of the page it shows.</param>
    /// <returns>The plugin entry.</returns>
    public static LocalPluginInfo PagedPluginInfo(Guid id, string name, string description, string pageName)
        => LocalPlugin(typeof(PagedPlugin), id, "SomePlugin.dll", canUninstall: true, name, plugin: new PagedPlugin(pageName), description: description);

    /// <summary>
    ///   A copy of a plugin entry that is installed but not loaded, so it has
    ///   no instance.
    /// </summary>
    public static LocalPluginInfo InactivePluginInfo(LocalPluginInfo pluginInfo)
        => LocalPlugin(pluginInfo.PluginType!, pluginInfo.ID, pluginInfo.DLLs[0], pluginInfo.CanUninstall, pluginInfo.Name, active: false);

    private static LocalPluginInfo LocalPlugin(
        Type pluginType,
        Guid id,
        string dllName,
        bool canUninstall,
        string name,
        IPlugin? plugin = null,
        bool active = true,
        string? containingDirectory = null,
        string description = "",
        IReadOnlyList<string>? tags = null,
        string? authors = null
    )
        => new()
        {
            ID = id,
            Name = name,
            Description = description,
            Version = Version(),
            Authors = authors,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = tags ?? [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = DateTime.UnixEpoch,
            // An entry that is loaded is enabled; the tests asking about a toggle need to see
            // the toggle actually refused rather than a default they never set.
            IsEnabled = active,
            IsActive = active,
            CanUninstall = canUninstall,
            Plugin = active ? plugin : null,
            PluginType = pluginType,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = containingDirectory,
            DLLs = new List<string> { dllName },
            Types = [],
            Dependencies = []
        };

    private static VersionInformation Version()
        => new()
        {
            Version = new Version(1, 0, 0, 0),
            RuntimeIdentifier = "win-x64",
            AbstractionVersion = new Version(6, 0, 0, 0),
            SourceRevision = null,
            ReleaseTag = null,
            Channel = ReleaseChannel.Stable,
            ReleasedAt = DateTime.UnixEpoch
        };

    /// <summary>
    ///   A plugin type to attribute a part to when the test needs one that is not the core.
    /// </summary>
    public sealed class TestPlugin : IPlugin
    {
        public Guid ID { get; } = Guid.NewGuid();

        public string Name => "Some Plugin";
    }

    /// <summary>
    ///   A plugin showing one page.
    /// </summary>
    /// <param name="pageName">The name of the page.</param>
    public sealed class PagedPlugin(string pageName) : IPlugin
    {
        public Guid ID { get; } = Guid.NewGuid();

        public string Name => "Paged Plugin";

        public IReadOnlyList<PluginPage> GetPages()
            => [new() { Name = pageName, Url = $"/{pageName}" }];
    }
}
