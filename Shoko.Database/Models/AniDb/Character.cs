using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Character")]
    public class Character
    {
        [Key, Column("AniDB_CharacterID")] public int Id { get; set; }
        public int CharId { get; set; }
        [Column("CharName")] public string Name { get; set; }
        [MaxLength(100)] public string PicName { get; set; }
        [Column("CharKanjiName")] public string KanjiName { get; set; }
        [Column("CharDescription")] public string Description { get; set; }
        public string CreatorListRaw { get; set; }
    }
}
