using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Recommendation")]
    public class Recommendation
    {
        [Key, Column("AniDB_RecommendationID")] public int Id { get; set; }
        public int AnimeID { get; set; }
        public int UserID { get; set; }
        [Column("RecommendationType")] public int Type { get; set; }
        [Column("RecommendationText")] public string Text { get; set; }
    }
}
