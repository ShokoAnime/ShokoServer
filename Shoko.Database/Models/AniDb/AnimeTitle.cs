using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Anime_Title")]
    public class AnimeTitle
    {
        [Key, Column("AniDB_Anime_TitleID")]
        public int Id { get; set; }
        public int AnimeId { get; set; }
        [MaxLength(50)] public string TitleType { get; set; }
        [MaxLength(50)] public string Language { get; set; }
        [MaxLength(500)] public string Title { get; set; }
    }
}