using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class FileFfdshowPreset
    {
        [Key, Column("FileFfdshowPresetID")] public int Id { get; set; }
        [Required, MaxLength(50)] public string Hash { get; set; }
        public long FileSize { get; set; }
        public string Preset { get; set; }
    }
}
