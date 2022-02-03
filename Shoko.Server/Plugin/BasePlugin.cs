using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin
{
    public class BasePlugin : IPlugin
    {
        public string Name => "Shoko Base";
        
        
        public void Load()
        {
        }

        public void OnSettingsLoaded(IPluginSettings settings)
        {
            // No settings!? Maybe make this abstract in the future
        }
    }
}