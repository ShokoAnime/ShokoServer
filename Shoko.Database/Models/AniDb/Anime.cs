using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime")]
    public class Anime
    {
        [Key, Column("AniDB_AnimeID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime AirDate { get; set; }
        public DateTime EndDate { get; set; }

        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public int AnimeType { get; set; }

        [MaxLength(500)] public string MainTitle { get; set; }
        [MaxLength(1500)] public string AllTitles { get; set; }
        public string AllTags { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }

        [Required] public DateTime DateTimeUpdated { get; set; }
        [Required] public DateTime DateTimeDescUpdated { get; set; }

        public bool ImageEnabled { get; set; }
        public string AwardList { get; set; }
        public bool Restricted { get; set; }

        #region Other IDs
        public int? AnimePlanetId { get; set; }
        public int? ANNId { get; set; }
        public int? AllCinemaId { get; set; }
        public int? AnimeNfo { get; set; }
        [Column("Site_JP")] public string SiteJP { get; set; }
        [Column("Site_EN")] public string SiteEN { get; set; }

        [Column("Wikipedia_ID")] public string Wikipedia { get; set; }
        [Column("WikipediaJP_ID")] public string WikipediaJP { get; set; }

        public int? SyoboiID { get; set; }
        public int? AnisonID { get; set; }
        public int? CrunchyrollID { get; set; }
        #endregion

        public int? LatestEpisodeNumber { get; set; }
        public bool DisableExternalLinksFlag { get; set; }
        public int ContractVersion { get; set; } = 0;
        public byte[] ContractBlob { get; set; } = null;
    }
}
