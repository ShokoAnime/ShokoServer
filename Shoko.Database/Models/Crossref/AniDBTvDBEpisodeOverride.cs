using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_TvDB_Episode_Override") ]
    public class AniDBTvDBEpisodeOverride
    {
        [Key, Column("CrossRef_AniDB_TvDB_Episode_OverrideID")] public int Id { get; set; }
        public int AniDBEpisodeID { get; set; }
        public int TvDBEpisodeID { get; set; }
    }
}
