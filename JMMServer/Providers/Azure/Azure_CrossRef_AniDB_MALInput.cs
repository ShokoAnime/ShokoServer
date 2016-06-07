using JMMServer.Entities;

namespace JMMServer.Providers
{
    public class CrossRef_AniDB_MALInput
    {
        public CrossRef_AniDB_MALInput()
        {
        }

        public CrossRef_AniDB_MALInput(CrossRef_AniDB_MAL xref)
        {
            AnimeID = xref.AnimeID;
            MALID = xref.MALID;
            CrossRefSource = xref.CrossRefSource;
            MALTitle = xref.MALTitle;
            StartEpisodeType = xref.StartEpisodeType;
            StartEpisodeNumber = xref.StartEpisodeNumber;

            Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                Username = Constants.AnonWebCacheUsername;
        }

        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
    }
}