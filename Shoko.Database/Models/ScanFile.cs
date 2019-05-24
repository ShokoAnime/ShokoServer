using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class ScanFile
    {
        [Key, Column("ScanFileID")] public int Id { get; set; }
        [ForeignKey(nameof(Scan))] public int ScanID { get; set; }
        [ForeignKey(nameof(ImportFolder))] public int ImportFolderId { get; set; }
        [ForeignKey(nameof(VideoLocalPlace))] public int VideoLocalPlaceId { get; set; }
        public string FullName { get; set; }
        public long FileSize { get; set; }
        public DateTime? CheckDate { get; set; }
        public string Hash { get; set; }
        public string HashResult { get; set; }

        public virtual Scan Scan { get; set; }
        public virtual ImportFolder ImportFolder { get; set; }
        public virtual VideoLocalPlace VideoLocalPlace { get; set; }
    }
}
