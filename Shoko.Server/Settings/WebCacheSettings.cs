namespace Shoko.Server.Settings
{
    public class WebCacheSettings
    {
        public string Address { get; set; } = "omm.hobbydb.net.leaf.arvixe.com";

        public bool Anonymous { get; set; } = false;

        public bool XRefFileEpisode_Get { get; set; } = true;

        public bool XRefFileEpisode_Send { get; set; } = true;

        public bool TvDB_Get { get; set; } = true;

        public bool TvDB_Send { get; set; } = true;

        public bool Trakt_Get { get; set; } = true;

        public bool Trakt_Send { get; set; } = true;

        public bool UserInfo { get; set; } = true;
        public string AuthKey { get; set; } = string.Empty;
    }
}