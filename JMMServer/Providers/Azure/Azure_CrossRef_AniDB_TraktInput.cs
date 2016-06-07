using JMMServer.Entities;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_TraktInput
    {
        public CrossRef_AniDB_TraktInput()
        {
        }

        public CrossRef_AniDB_TraktInput(CrossRef_AniDB_TraktV2 xref, string animeName)
        {
            AnimeID = xref.AnimeID;
            AnimeName = animeName;
            AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
            AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
            TraktID = xref.TraktID;
            TraktSeasonNumber = xref.TraktSeasonNumber;
            TraktStartEpisodeNumber = xref.TraktStartEpisodeNumber;
            TraktTitle = xref.TraktTitle;
            CrossRefSource = xref.CrossRefSource;

            Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                Username = Constants.AnonWebCacheUsername;

            AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
        }

        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string AuthGUID { get; set; }
    }
}