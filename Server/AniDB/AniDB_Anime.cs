using System;


namespace Shoko.Models.Server
{
    public class AniDB_Anime
    {
        #region Server DB columns

        public int AniDB_AnimeID { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public int AnimeType { get; set; }
        public string MainTitle { get; set; }
        public string AllTitles { get; set; }
        public string AllTags { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        [Obsolete("Deprecated in favor of AniDB_AnimeUpdate. This is for when an AniDB_Anime fails to save")]
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeDescUpdated { get; set; }
        public int ImageEnabled { get; set; }
        public string AwardList { get; set; }
        public int Restricted { get; set; }
        public int? AnimePlanetID { get; set; }
        public int? ANNID { get; set; }
        public int? AllCinemaID { get; set; }
        public int? AnimeNfo { get; set; }
        public int? AnisonID { get; set; }
        public int? SyoboiID { get; set; }
        public string Site_JP { get; set; }
        public string Site_EN { get; set; }
        public string Wikipedia_ID { get; set; }
        public string WikipediaJP_ID { get; set; }
        public string CrunchyrollID { get; set; }
        public int? LatestEpisodeNumber { get; set; }
        public int DisableExternalLinksFlag { get; set; }

        #endregion

    }
}
