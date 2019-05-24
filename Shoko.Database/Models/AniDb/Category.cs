using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Category")]
    public class Category
    {
        [Key, Column("AniDB_CategoryID")] public int Id { get; set; }
        public int CategoryID { get; set; }
        public int ParentId { get; set; }
        public bool IsHentai { get; set; }
        [Column("CategoryName")] public string Name { get; set; }
        [Column("CategoryDescription")] public string Description { get; set; }
    }
}
