using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class Language
    {
        [Key, Column("LanguageID")] public int Id { get; set; }
        [Column("LanguageName")] public string Name { get; set; }
    }
}
