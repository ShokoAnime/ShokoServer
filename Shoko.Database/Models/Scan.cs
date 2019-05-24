using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class Scan
    {
        [Key, Column("ScanID")] public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string ImportFolders { get; set; }
        public int Status { get; set; }
    }
}
