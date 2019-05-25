using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_Trakt")]
    public class AniDbTrakt
    {
        [Key, Column("CrossRef_AniDB_TraktID")] public int Id { get; set; }
        public int AnimeID { get; set; }
        [MaxLength(100)] public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }
    }
}