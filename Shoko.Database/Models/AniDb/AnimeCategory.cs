using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Category")]
    public class AnimeCategory
    {
        [Key, Column("AniDB_Anime_CategoryID")] public int Id { get; set; }
        public int AnimeId { get; set; } //Compound Key Candidate.
        public int CategoryId { get; set; } //Compound Key Candidate. (& FK to AniDB_Anime.AnimeId)
    }
}
