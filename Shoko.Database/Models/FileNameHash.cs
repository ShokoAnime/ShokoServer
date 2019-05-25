using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class FileNameHash
    {
        [Key, Column("FileNameHashID")] public int Id { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        [MaxLength(50)] public string Hash { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}
