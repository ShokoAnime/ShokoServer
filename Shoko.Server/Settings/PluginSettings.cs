using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Settings;

public class PluginSettings
{
    public Dictionary<string, bool> EnabledPlugins { get; set; } = new();

    public List<string> Priority { get; set; } = new();
    public Dictionary<string, bool> EnabledRenamers { get; set; } = new();
    public Dictionary<string, int> RenamerPriorities { get; set; } = new();

    [JsonIgnore] public List<IPluginSettings> Settings { get; set; } = new();

    public bool DeferOnError { get; set; }
}
