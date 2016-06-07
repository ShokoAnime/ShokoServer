using JMMServer.Entities;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_TvDBInput
    {
        public CrossRef_AniDB_TvDBInput()
        {
        }

        public CrossRef_AniDB_TvDBInput(CrossRef_AniDB_TvDBV2 xref, string animeName)
        {
            AnimeID = xref.AnimeID;
            AnimeName = animeName;
            AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
            AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
            TvDBID = xref.TvDBID;
            TvDBSeasonNumber = xref.TvDBSeasonNumber;
            TvDBStartEpisodeNumber = xref.TvDBStartEpisodeNumber;
            TvDBTitle = xref.TvDBTitle;
            CrossRefSource = xref.CrossRefSource;

            Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                Username = Constants.AnonWebCacheUsername;

            AuthGUID = string.Empty;
        }

        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }
        public string TvDBTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string AuthGUID { get; set; }
    }
}