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
    ///   A plugin entry for an ordinary, uninstallable plugin.
    /// </summary>
    public static LocalPluginInfo InstalledPluginInfo(Type pluginType, Guid id, string dllName = "SomePlugin.dll")
        => LocalPlugin(pluginType, id, dllName, canUninstall: true, name: "Some Plugin");

    private static LocalPluginInfo LocalPlugin(Type pluginType, Guid id, string dllName, bool canUninstall, string name)
        => new()
        {
            ID = id,
            Name = name,
            Description = string.Empty,
            Version = Version(),
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = DateTime.UnixEpoch,
            // An entry that is loaded is enabled; the tests asking about a toggle need to see
            // the toggle actually refused rather than a default they never set.
            IsEnabled = true,
            IsActive = true,
            CanUninstall = canUninstall,
            Plugin = null,
            PluginType = pluginType,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
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
}
