using System.Collections.Generic;

namespace Shoko.Server.Settings
{
    public class PluginSettings
    {
        public Dictionary<string, bool> EnabledPlugins { get; set; } = new Dictionary<string, bool>();
    }
}