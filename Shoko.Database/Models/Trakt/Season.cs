using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Trakt
{
    [Table("Trakt_Season")]
    public class Season
    {
        [Key, Column("Trakt_SeasonID")] public int Id { get; set; }
        [Column("Trakt_ShowID"), ForeignKey(nameof(Show))] public int ShowID { get; set; }
        [Column("Season")] public int SeasonNumber { get; set; }
        public string Url { get; set; }

        public virtual Show Show { get; set; }
    }
}
