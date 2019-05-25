using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_Languages_AniDB_File")]
    public class LanguagesAniDBFile
    {
        [Key, Column("CrossRef_Languages_AniDB_FileID")] public int Id { get; set; }
        public int FileID { get; set; }
        public int LanguageID { get; set; }
    }
}
