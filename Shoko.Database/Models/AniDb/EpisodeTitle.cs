using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Episode_Title")]
    public class EpisodeTitle
    {
        [Key, Column("AniDB_Episode_TitleID")] public int Id { get; set; }
        [Column("AniDB_EpisodeID")] public int EpisodeID { get; set; }
        [MaxLength(50)] public string Language { get; set; }
        [MaxLength(500)] public string Title { get; set; }
    }
}
