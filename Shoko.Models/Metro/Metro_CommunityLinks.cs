namespace Shoko.Models.Metro
{
    public class Metro_CommunityLinks
    {
        public int AniDB_ID { get; set; } // AnimeID
        public string Trakt_ID { get; set; }
        public string MAL_ID { get; set; }
        public string TvDB_ID { get; set; }
        public string CrunchyRoll_ID { get; set; }
        public string Youtube_ID { get; set; }

        public string AniDB_URL { get; set; }
        public string Trakt_URL { get; set; }
        public string MAL_URL { get; set; }
        public string TvDB_URL { get; set; }
        public string CrunchyRoll_URL { get; set; }
        public string Youtube_URL { get; set; }

        public string AniDB_DiscussURL { get; set; }
        public string Trakt_DiscussURL { get; set; }
        public string MAL_DiscussURL { get; set; }
        public string TvDB_DiscussURL { get; set; }
        public string CrunchyRoll_DiscussURL { get; set; }
        public string Youtube_DiscussURL { get; set; }
    }
}