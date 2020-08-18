using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Settings
{
    public class PluginSettings
    {
        public Dictionary<string, bool> EnabledPlugins { get; set; } = new Dictionary<string, bool>();
        
        public List<string> Priority { get; set; } = new List<string>();

        [JsonIgnore]
        public List<IPluginSettings> Settings { get; set; } = new List<IPluginSettings>();
    }
}