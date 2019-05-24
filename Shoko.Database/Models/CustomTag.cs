using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class CustomTag
    {
        [Key, Column("CustomTagID")] public int Id { get; set; }
        [Required, Column("TagName")] public string Name { get; set; }
        [Column("TagDescription")] public string Description { get; set; }
    }
}
