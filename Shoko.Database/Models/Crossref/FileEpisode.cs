using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_File_Episode")]
    class FileEpisode
    {
        [Key, Column("CrossRef_File_EpisodeID")] public int Id { get; set; }
        [MaxLength(50)] public string Hash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int CrossRefSource { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        [Range(0, 100)] public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
    }
}
