using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

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
