using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Character_Seiyuu")]
    public class CharacterSeiyuu
    {
        [Key, Column("AniDB_Character_SeiyuuID")] public int Id { get; set; }
        public int CharId { get; set; }
        public int SeiyuuId { get; set; }
    }
}
