using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Server.Settings
{
    public class TvDBSettings : IDefaultedConfig
    {
        public bool AutoLink { get; set; } = false;

        public bool AutoFanart { get; set; } = true;

        public int AutoFanartAmount { get; set; } = 10;

        public bool AutoWideBanners { get; set; } = true;

        public int AutoWideBannersAmount { get; set; } = 10;

        public bool AutoPosters { get; set; } = true;

        public int AutoPostersAmount { get; set; } = 10;

        public ScheduledUpdateFrequency UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public string Language { get; set; } = "en";
        public void SetDefaults()
        {
        }
    }
}