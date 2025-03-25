using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

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

    public RenamerSettings Renamer { get; set; } = new();
}
