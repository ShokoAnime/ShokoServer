using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    [Table("VideoLocal_Place")]
    public class VideoLocalPlace
    {
        [Key, Column("VideoLocal_Place_ID")] public int VideoLocalPlaceId { get; set; }
        [Required] public int VideoLocalId { get; set; }
        public string FilePath { get; set; }
        [Required] public int ImportFolderId { get; set; }
        [Required] public int ImportFolderType { get; set; }

        public virtual ImportFolder ImportFolder { get; set; }
        public virtual VideoLocal VideoLocal { get; set; }
    }
}
