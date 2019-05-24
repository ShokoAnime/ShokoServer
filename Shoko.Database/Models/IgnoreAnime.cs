using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class IgnoreAnime
    {
        [Key, Column("IgnoreAnimeID")] public int Id { get; set; }
        [ForeignKey(nameof(User)), Column("JMMUserID")] public int UserId { get; set; }
        public int AnimeID { get; set; } //FK To AniDB_Anime.AnimeID
        public int IgnoreType { get; set; }

        public virtual ShokoUser User { get; set; }
    }
}
