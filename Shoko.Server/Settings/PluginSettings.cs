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
    [Visibility(Visibility = DisplayVisibility.ReadOnly)]
    [Display(Name = "Enabled Plugins")]
    public Dictionary<string, bool> EnabledPlugins { get; set; } = [];

    /// <summary>
    /// Load order of plugins. It will show both enabled and disabled, but
    /// disabled plugins will not be loaded.
    /// </summary>
    [Display(Name = "Plugin Load Order")]
    [Visibility(Visibility = DisplayVisibility.ReadOnly)]
    public List<string> Priority { get; set; } = [];

    public RenamerSettings Renamer { get; set; } = new();
}
