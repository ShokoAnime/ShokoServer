using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Tag")]
    public class AnimeTag
    {
        [Key, Column("AniDB_Anime_TagID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        public int TagId { get; set; }
        public int Approval { get; set; }
        public int Weight { get; set; }
    }
}
