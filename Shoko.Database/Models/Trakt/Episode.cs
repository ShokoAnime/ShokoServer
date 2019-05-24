using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Trakt
{
    [Table("Trakt_Episode")]
    public class Episode
    {
        [Key, Column("Trakt_EpisodeID")] public int Id { get; set; }
        [Column("Trakt_ShowID"), ForeignKey(nameof(Show))] public int ShowID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }
        [MaxLength(500)] public string Title { get; set; }
        public string URL { get; set; }
        public string Overview { get; set; }
        [MaxLength(500)] public string EpisodeImage { get; set; }
        public int TraktID { get; set; }

        public virtual Show Show { get; set; }
    }
}
