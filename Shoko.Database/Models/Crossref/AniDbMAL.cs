using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_MAL")]
    public class AniDbMAL
    {
        [Key, Column("CrossRef_AniDB_MALID")]public int Id { get; set; }
        public int AnimeID { get; set; }
        public int MalID { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
        public int CrossRefSource { get; set; }
    }
}
