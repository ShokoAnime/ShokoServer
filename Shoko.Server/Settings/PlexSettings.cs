using System.Collections.Generic;

namespace Shoko.Server.Settings
{
    public class PlexSettings
    {
        public string ThumbnailAspects { get; set; } = "Default, 0.6667, IOS, 1.0, Android, 1.3333";

        public List<int> Libraries { get; set; } = new ();

        public string Token { get; set; } = string.Empty;

        public string Server { get; set; } = string.Empty;
    }
}