using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Tag")]
    public class Tag
    {
        [Key, Column("AniDB_TagID")] public int Id { get; set; }
        public int TagId { get; set; }
        public bool Spoiler { get; set; }
        public bool LocalSpoiler { get; set; }
        public bool GlobalSpoiler { get; set; }
        [Required, MaxLength(150), Column("TagName")] public string Name { get; set; }
        [Column("TagCount")] public int Count { get; set; }
        [Required, Column("TagDescription")] public string Description { get; set; }
    }
}
