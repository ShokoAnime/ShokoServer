namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_TvDBV2
    {
        public int CrossRef_AniDB_TvDBV2ID { get; set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }
        public string TvDBTitle { get; set; }

        public int CrossRefSource { get; set; }
        public bool IsAdditive { get; set; }

        public CrossRef_AniDB_TvDBV2()
        {
        }

    }
}
