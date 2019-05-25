using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_AniDB_Trakt_Episode")]
    public class AniDBTraktEpisode
    {
        [Key, Column("CrossRef_AniDB_Trakt_EpisodeID")]
        public int Id { get; set; }
        public int AnimeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        [MaxLength(100)] public int TraktID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }
    }
}