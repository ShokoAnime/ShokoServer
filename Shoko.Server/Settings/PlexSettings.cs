namespace Shoko.Server.Settings
{
    public class PlexSettings
    {
        public string ThumbnailAspects { get; set; } = "Default, 0.6667, IOS, 1.0, Android, 1.3333";

        public int[] Libraries { get; set; } = new int[0];

        public string Token { get; set; } = string.Empty;

        public string Server { get; set; } = string.Empty;
    }
}