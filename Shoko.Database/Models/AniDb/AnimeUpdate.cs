using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_AnimeUpdate")]
    public class AnimeUpdate
    {
        [Key, Column("AniDB_AnimeUpdateID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        [Required] public DateTime UpdatedAt { get; set; }
    }
}
