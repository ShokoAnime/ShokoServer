using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Relation")]
    public class AnimeRelation
    {
        [Key, Column("AniDB_Anime_RelationID")] public int Id { get; set; }
        public int AnimeId { get; set; } //PK & FK
        public int RelatedAnimeId { get; set; } //PK & FK
        [Column("RelationType")] public string Type { get; set; }
    }
}
