using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.TvDB
{
    [Table("TvDB_Episode")]
    //TODO: Fucking fix me... please.
    public class Episode
    {
        [Key] public int TvDB_EpisodeID { get; set; }
        public int Id { get; set; } //This should actually be the PK
        public int SeriesID { get; set; } //This is a FK to TvDB_Series.SeriesID... that should be the PK, not a useless PK.
        public int SeasonID { get; set; } //This is a FK to TvDB_Season.SeasonID... as above.
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string Overview { get; set; }
        public string FileName { get; set; }
        public int EpImgFlag { get; set; } //Bool?
        public int? AbsoluteNumber { get; set; }
        public int? AirsAfterSeason { get; set; }
        public int? AirsBeforeEpisode { get; set; }
        public int? AirsBeforeSeason { get; set; }
        public int? Rating { get; set; }
        public DateTime? AirDate { get; set; }
    }
}
