using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Similar")]
    public class AnimeSimilar
    {
        [Key, Column("AniDB_Anime_SimilarID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        public int SimilarAnimeId { get; set; }
        public int Approval { get; set; }
        public int Total { get; set; }
    }
}
