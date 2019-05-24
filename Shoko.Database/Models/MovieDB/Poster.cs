using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.MovieDB
{
    [Table("MovieDB_Poster")]
    public class Poster
    {
        [Key, Column("MovieDB_PosterID")] public int Id { get; set; }
        [MaxLength(100)] public string ImageId { get; set; }
        /*[ForeignKey(nameof(Movie))]*/ public int MovieID { get; set; } //Todo: FK once the PKs are updated right.
        [MaxLength(100)] public string ImageType { get; set; }
        [MaxLength(100)] public string ImageSize { get; set; }
        public string URL { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public bool Enabled { get; set; }


        /*public virtual Movie Movie { get; set; }*/
    }
}
