using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.MovieDB
{
    [Table("MovieDB_Movie")]
    public class Movie
    {
        [Key, Column("MovieDB_MovieID")] public int Id { get; set; }
        public int MovieID { get; set; } //Should be a PK
        [MaxLength(250), Column("MovieName")] public string Name { get; set; }
        [MaxLength(250)] public string OriginalName { get; set; }
        public string Overview { get; set; }
        public int Rating { get; set; }
    }
}