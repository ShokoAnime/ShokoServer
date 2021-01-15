using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Server.Settings
{
    public class LinuxSettings : IDefaultedConfig
    {
        public int UID { get; set; } = -1;
        public int GID { get; set; } = -1;
        public int Permission { get; set; } = 0;
        public void SetDefaults()
        {
            
        }
    }
}