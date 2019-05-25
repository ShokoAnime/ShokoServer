using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_TraktV2")]
    public class AniDBTraktEpisode
    {
        [Key, Column("CrossRef_AniDB_TraktV2ID")]
        public int Id { get; set; }
    }
}