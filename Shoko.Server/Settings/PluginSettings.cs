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
        ///   Whether automatic plugin upgrades are enabled.
        /// </summary>
        [DefaultValue(false)]
        [EnvironmentVariable("SHOKO_PLUGIN_AUTO_UPGRADE_ENABLED")]
        public bool IsAutoUpgradeEnabled { get; set; } = false;

        /// <summary>
        ///   Whether the server installs the newest compatible release of each
        ///   installed plugin while it starts, before the plugins load. Pinned
        ///   plugins are left alone. Independent of
        ///   <see cref="IsAutoUpgradeEnabled"/>.
        /// </summary>
        [DefaultValue(true)]
        [EnvironmentVariable("SHOKO_PLUGIN_UPGRADE_ON_START_ENABLED")]
        public bool IsUpgradeOnStartEnabled { get; set; } = true;

        /// <summary>
        ///   Whether the automatic upgrades, on start and while running, leave
        ///   the plugins the server ships alone, so they only change with the
        ///   server. Installing a release by hand still works.
        /// </summary>
        [DefaultValue(false)]
        [EnvironmentVariable("SHOKO_PLUGIN_SKIP_BUNDLED_UPGRADES")]
        public bool SkipBundledPluginUpgrades { get; set; } = false;

        /// <summary>
        ///   Default time before a repository's packages are considered stale.
        ///   Defaults to 12 hours.
        /// </summary>
        [DefaultValue("12:00:00.000000")]
        public TimeSpan DefaultRepositoryStaleTime { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        ///   How old a repository's packages may be when the server starts
        ///   before the upgrades on start fetch them again; the repository's
        ///   own stale time applies when shorter. Defaults to 5 minutes, so a
        ///   start soon after another does not fetch them again.
        /// </summary>
        [DefaultValue("00:05:00.000000")]
        public TimeSpan RepositoryStaleTimeOnStart { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        ///   The keys of the repositories shipped with the server that were
        ///   added once already. One listed here is not added again, so it
        ///   stays removed once an admin removed it.
        /// </summary>
        [Visibility(DisplayVisibility.Hidden)]
        public List<string> SeededRepositories { get; set; } = [];

        /// <summary>
        ///   How long an older plugin version stays installed after a newer
        ///   one was installed, before the "Purge Inactive Plugin Versions"
        ///   scheduled action marks it for removal. It counts from that
        ///   install, not from the restart that activated the newer version.
        ///   A <c>.keep</c> file protects a version. Zero removes it at the
        ///   next run. Defaults to 30 days.
        /// </summary>
        [DefaultValue("30.00:00:00.000000")]
        public TimeSpan InactivePluginVersionRetention { get; set; } = TimeSpan.FromDays(30);
    }
}
