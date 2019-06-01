using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Core.Models
{
    public class VideoFile
    {
        public Guid Id { get; set; }
        [Required] 
        public Uri Path { get; set; } //UriKind.Relative
        [Required, MaxLength(50)] 
        public string E2DK { get; set; }
        [MaxLength(50)]
        public string CRC32 { get; set; }
        [MaxLength(50)] 
        public string MD5 { get; set; }
        [MaxLength(50)] 
        public string SHA1 { get; set; }

        [Required, ForeignKey(nameof(DataFolder))] 
        public Guid DataFolderId { get; set; }


        public string ProviderType { get; set; }

        public virtual DataFolder DataFolder { get; set; }
    }
}