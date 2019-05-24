using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class BookmarkedAnime
    {
        [Key, Column("BookmarkedAnimeID")] public int Id { get; set; }
        [Column("AnimeID")] public int AnimeId { get; set; } //this has a unique constraint, we could use this for the PK
        public int Priority { get; set; }
        public string Notes { get; set; }
        public bool Download { get; set; }
    }
}
