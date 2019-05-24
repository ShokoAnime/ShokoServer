using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Review")]
    public class AnimeReview
    {
        [Key, Column("AniDB_Anime_ReviewID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        public int ReviewId { get; set; }
    }
}
