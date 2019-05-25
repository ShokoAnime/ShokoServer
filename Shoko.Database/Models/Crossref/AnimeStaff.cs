using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_Anime_Staff")]
    public class AnimeStaff
    {
        [Key, Column("CrossRef_Anime_Staff")] public int Id { get; set; }
        public int AniDB_AnimeID { get; set; }
        public int StaffID { get; set; }
        public string Role { get; set; }
        public int RoleID { get; set; }
        public int RoleType { get; set; }
        public string Language { get; set; }
    }
}
