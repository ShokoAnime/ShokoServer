using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Crossref
{
    [Table("CrossRef_CustomTag")]
    public class CustomTag
    {
        [Key, Column("CrossRef_CustomTagID")] public int Id { get; set; }
        public int CustomTagID { get; set; }
        public int CrossRefID { get; set; }
        public int CrossRefType { get; set; }
    }
}
