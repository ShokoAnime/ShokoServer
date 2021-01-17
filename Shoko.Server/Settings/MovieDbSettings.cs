namespace Shoko.Server.Settings
{
    public class MovieDbSettings
    {
        public bool AutoFanart { get; set; } = true;

        public int AutoFanartAmount { get; set; } = 10;

        public bool AutoPosters { get; set; } = true;

        public int AutoPostersAmount { get; set; } = 10;
    }
}