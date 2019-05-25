using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_TvDB")]
    public class AniDBTvDB
    {
        [Key, Column("CrossRef_AniDB_TvDBID")] public int Id { get; set; }
        public int TvDBID { get; set; }
        public int CrossRefSource { get; set; }
    }
}
