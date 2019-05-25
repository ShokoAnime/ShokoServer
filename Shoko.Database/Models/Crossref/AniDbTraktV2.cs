using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_TraktV2")]
    public class AniDbTraktV2
    {
        [Key, Column("CrossRef_AniDB_TraktV2")] public int ID { get; set; }
        public int AnimeID { get; set; }
        public int AniDBStaretEpisodeType { get; set; }
        public int AniDBStaretEpisodeNumber { get; set; }
        [MaxLength(100)] public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
    }
}