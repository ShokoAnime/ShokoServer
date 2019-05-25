using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class DuplicateFile
    {
        [Key, Column("DuplicateFileID")] public int Id { get; set; }
        [Column("FilePathFile1")] public string FilePath1 { get; set; }
        [Column("FilePathFile1")] public string FilePath2 { get; set; }
        [Column("ImportFolderIDFile1"), ForeignKey(nameof(ImportFolder1))] public int ImportFolderId1 { get; set; }
        [Column("ImportFolderIDFile2"), ForeignKey(nameof(ImportFolder2))] public int ImportFolderId2 { get; set; }
        [MaxLength(50)] public string Hash { get; set; }
        public DateTime DateTimeUpdated { get; set; }

        public virtual ImportFolder ImportFolder1 { get; set; }
        public virtual ImportFolder ImportFolder2 { get; set; }
    }
}
