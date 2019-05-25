using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_Subtitles_AniDB_File")]
    public class SubtitlesAniDBFile
    {
        [Key, Column("CrossRef_Subtitles_AniDB_FileID")] public int Id { get; set; }
        public int FileID { get; set; }
        public int LanguageID { get; set; }
    }
}
