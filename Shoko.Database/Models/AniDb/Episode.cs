using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Episode")]
    public class Episode
    {
        [Key, Column("AniDB_EpisodeID")] public int Id { get; set; }
        public int EpisodeId { get; set; }
        public int AnimeId { get; set; }
        public int LengthSeconds { get; set; }
        [MaxLength(200)] public string Rating { get; set; }
        [MaxLength(200)] public string Votes { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }
        public int AirDate { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public string Description { get; set; }
    }
}
