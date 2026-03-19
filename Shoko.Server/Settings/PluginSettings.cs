using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.Server;

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
    public RenamerSettings Renamer { get; set; } = new();

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
        ///   How often to automatically check for and apply plugin updates if enabled.
        ///   Defaults to every 6 hours.
        /// </summary>
        [DefaultValue(ScheduledUpdateFrequency.HoursSix)]
        public ScheduledUpdateFrequency AutoUpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursSix;

        /// <summary>
        ///   Default time before a repository's packages are considered stale.
        ///   Defaults to 12 hours.
        /// </summary>
        [DefaultValue("12:00:00.000000")]
        public TimeSpan DefaultRepositoryStaleTime { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        ///   Time to retain old plugin versions before auto-cleanup. Defaults to
        ///   30 days.
        /// </summary>
        [DefaultValue("720.00:00.000000")]
        public TimeSpan InactivePluginVersionRetention { get; set; } = TimeSpan.FromDays(30);
    }
}
