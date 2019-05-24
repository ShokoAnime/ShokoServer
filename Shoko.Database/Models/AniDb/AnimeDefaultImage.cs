using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_DefaultImage")]
    public class AnimeDefaultImage
    {
        [Key, Column("AniDB_Anime_DefaultImageID")] public int Id { get; set; }
        public int AnimeId { get; set; }
        [Column("ImageParentId")] public int ParentId { get; set; }
        [Column("ImageParentType")] public int ParentType { get; set; }
        [Column("ImageType")] public int Type { get; set; }
    }
}
