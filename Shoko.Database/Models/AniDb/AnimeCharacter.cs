using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Character")]
    public class AnimeCharacter
    {
        [Key, Column("AniDB_Anime_CharacterID")] public int Id { get; set; }
        public int AnimeId { get; set; } //PK & FK
        public int CharId { get; set; }  //PK & FK
        [MaxLength(100)] public string CharType { get; set; }
        public string EpisodeListRaw { get; set; }

    }
}
