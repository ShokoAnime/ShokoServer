using System;

namespace Shoko.Server.Settings
{
    public class WebCacheSettings
    {
        public bool Enabled { get; set; } = false;
        public string Address { get; set; } = "https://localhost:44307";

        public string BannedReason { get; set; }

        public DateTime? BannedExpiration { get; set; }

        public bool XRefFileEpisode_Get { get; set; } = true;

        public bool XRefFileEpisode_Send { get; set; } = true;

        public bool TvDB_Get { get; set; } = true;

        public bool TvDB_Send { get; set; } = true;

        public bool Trakt_Get { get; set; } = true;

        public bool Trakt_Send { get; set; } = true;
    }
}