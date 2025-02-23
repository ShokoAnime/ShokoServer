namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_TraktV2
    {
        public int CrossRef_AniDB_TraktV2ID { get; set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
    }
}
