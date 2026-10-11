using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Settings;

public class PluginSettings
{
    /// <summary>
    /// A list of all known plugins, with their enabled state.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Enabled Plugins")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_ENABLED_PLUGINS", AllowOverride = true)]
    [Record(HideAddAction = true, HideRemoveAction = true)]
    public Dictionary<string, bool> EnabledPlugins { get; set; } = [];

    /// <summary>
    /// Load order of plugins. It will show both enabled and disabled, but
    /// disabled plugins will not be loaded.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Plugin Load Order")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_PLUGIN_LOAD_ORDER", AllowOverride = true)]
    [List(UniqueItems = true, Sortable = true, HideAddAction = true, HideRemoveAction = true)]
    public List<string> Priority { get; set; } = [];

    /// <summary>
    ///   Lists the known plugins as <see cref="EnabledPlugins"/> keys.
    /// </summary>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <returns>One option per known plugin.</returns>
    [OptionsProvider(nameof(EnabledPlugins), Target = OptionsTarget.Keys)]
    public IEnumerable<SelectOption<string>> ListEnabledPluginsKeys(IPluginManager pluginManager)
        => ListPlugins(pluginManager);

    /// <summary>
    ///   Lists the known plugins as <see cref="Priority"/> entries.
    /// </summary>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <returns>One option per known plugin.</returns>
    [OptionsProvider(nameof(Priority))]
    public IEnumerable<SelectOption<string>> ListPriorityEntries(IPluginManager pluginManager)
        => ListPlugins(pluginManager);

    /// <summary>
    ///   Lists every plugin by the name of its main DLL, which the settings
    ///   are keyed by, labelled with its display name. A name the settings
    ///   still hold for a plugin no longer installed is labelled as itself.
    /// </summary>
    private IEnumerable<SelectOption<string>> ListPlugins(IPluginManager pluginManager)
    {
        var labels = pluginManager.GetPluginInfos()
            .GroupBy(info => Path.GetFileNameWithoutExtension(info.DLLs[0]))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(info => info.IsActive).First().Name);
        return labels.Keys
            .Union(Priority)
            .Union(EnabledPlugins.Keys)
            .Select(name => new SelectOption<string>(name, labels.GetValueOrDefault(name, name)));
    }

    /// <summary>
    ///   Settings for renamers.
    /// </summary>
    public RelocationSettings Renamer { get; set; } = new();

    /// <summary>
    ///   Settings for plugin updates.
    /// </summary>
    public PluginUpdatesSettings Updates { get; set; } = new();

    public class PluginUpdatesSettings
    {
        /// <summary>
        ///   Whether automatic repository syncing is enabled.
        /// </summary>
        [DefaultValue(false)]
        public bool IsAutoSyncEnabled { get; set; } = false;

        /// <summary>
        ///   Whether automatic plugin upgrades are enabled.
        /// </summary>
        [DefaultValue(false)]
        public bool IsAutoUpgradeEnabled { get; set; } = false;

        /// <summary>
        ///   Default time before a repository's packages are considered stale.
        ///   Defaults to 12 hours.
        /// </summary>
        [DefaultValue("12:00:00.000000")]
        public TimeSpan DefaultRepositoryStaleTime { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        ///   How long an older plugin version stays installed after a newer
        ///   one was installed, before the "Purge Inactive Plugin Versions"
        ///   scheduled action marks it for removal. It counts from that
        ///   install, not from the restart that activated the newer version.
        ///   A <c>.keep</c> file protects a version. Zero removes it at the
        ///   next run. Defaults to 30 days.
        /// </summary>
        [DefaultValue("720.00:00.000000")]
        public TimeSpan InactivePluginVersionRetention { get; set; } = TimeSpan.FromDays(30);
    }
}
