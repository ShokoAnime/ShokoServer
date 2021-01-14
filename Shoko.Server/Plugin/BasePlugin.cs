using Microsoft.Extensions.Configuration;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin
{
    public class BasePlugin : IPlugin
    {
        public void LoadSettings(IConfigurationSection settings)
        {
            // noop
        }

        public string Name => "Shoko Base";
        
        
        public void Load()
        {
        }
    }
}