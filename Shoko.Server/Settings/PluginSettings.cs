using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Settings
{
    public class PluginSettings
    {
        public Dictionary<string, bool> EnabledPlugins { get; set; } = new Dictionary<string, bool>();
        
        public List<string> Priority { get; set; } = new List<string>();
        public Dictionary<string, bool> EnabledRenamers { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> RenamerPriorities { get; set; } = new Dictionary<string, int>();

        public Dictionary<string, object> Settings { get; set; } = new ();
    }
}