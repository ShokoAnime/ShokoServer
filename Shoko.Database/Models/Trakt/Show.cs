using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.Trakt
{
    [Table("Trakt_Show")]
    public class Show
    {
        [Key, Column("Trakt_ShowID")] public int Id { get; set; }
        [MaxLength(100)] public string TraktID { get; set; }
        [MaxLength(500)] public string Title { get; set; }
        [MaxLength(50)] public string Year { get; set; } //Do we reasonably think we are going to hit a year with 50 digits?
        public string URL { get; set; }
        public string Overview { get; set; }
        [Column("TvDB_ID")] public int TvDBId { get; set; }
    }
}