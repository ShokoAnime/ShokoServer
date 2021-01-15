using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Server.Settings
{
    public class MovieDbSettings : IDefaultedConfig
    {
        public bool AutoFanart { get; set; } = true;

        public int AutoFanartAmount { get; set; } = 10;

        public bool AutoPosters { get; set; } = true;

        public int AutoPostersAmount { get; set; } = 10;
        public void SetDefaults()
        {
            
        }
    }
}