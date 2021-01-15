using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Server.Settings
{
    public class PluginSettings : IDefaultedConfig
    {
        public Dictionary<string, bool> EnabledPlugins { get; set; } = new();
        
        public HashSet<string> Priority { get; set; } = new();
        public Dictionary<string, bool> EnabledRenamers { get; set; } = new ();
        public Dictionary<string, int> RenamerPriorities { get; set; } = new();

        public Dictionary<string, object> Settings { get; set; } = new ();
        public void SetDefaults()
        {
        }
    }
}