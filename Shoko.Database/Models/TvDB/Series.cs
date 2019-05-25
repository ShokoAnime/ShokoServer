using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.TvDB
{
    [Table("TvDB_Series")]
    public class Series
    {
        [Key, Column("TvDB_SeriesID")] public int Id { get; set; }
        public int SeriesID { get; set; }
        public string Overview { get; set; }
        public string SeriesName { get; set; }
        [MaxLength(100)] public string Status { get; set; }
        [MaxLength(100)] public string Banner { get; set; }
        [MaxLength(100)] public string Fanart { get; set; }
        [MaxLength(100)] public string Poster { get; set; }
        [MaxLength(100)] public string Lastupdated { get; set; }
        public int Rating { get; set; }
    }
}