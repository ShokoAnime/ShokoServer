using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Settings;

public class PluginSettings
{
    public Dictionary<string, bool> EnabledPlugins { get; set; } = new();

    public List<string> Priority { get; set; } = new();
    public RenamerSettings Renamer { get; set; } = new();

    [JsonIgnore] public List<IPluginSettings> Settings { get; set; } = new();
}
