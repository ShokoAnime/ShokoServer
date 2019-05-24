using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Seiyuu")]
    public class Seiyuu
    {
        [Key, Column("AniDB_SeiyuuID")] public int Id { get; set; }
        public int SeiyuuId { get; set; }
        [Column("SeiyuuName")] public string Name { get; set; }
        [MaxLength(100)] public string PicName { get; set; }
    }
}
