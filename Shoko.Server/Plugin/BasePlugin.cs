using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin
{
    public class BasePlugin : IPlugin
    {
        public string Name => "Shoko Base";
        
        
        public void Load()
        {
        }
    }
}