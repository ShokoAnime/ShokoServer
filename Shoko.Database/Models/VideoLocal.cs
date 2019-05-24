using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Database.Models
{
    public class VideoLocal
    {
        [Key] public int VideoLocalId { get; set; }

        [Required, MaxLength(50)] public string Hash { get; set; }
        [MaxLength(50)] public string CRC32 { get; set; }
        [MaxLength(50)] public string MD5 { get; set; }
        [MaxLength(50)] public string SHA1 { get; set; }
        [Required] public int HashSource { get; set; }
        [Required] public long FileSize { get; set; }
        [Required] public bool IsIgnored { get; set; } = false;

        [Required] public DateTime DateTimeUpdated { get; set; }
        [Required] public DateTime DateTimeCreated { get; set; }
        [Required] public bool IsVariation { get; set; }
        [Required] public int MediaVersion { get; set; } = 0;
        public byte[] MediaBlob { get; set; }
        [Required] public int MediaSize { get; set; } = 0;
        public string FileName { get; set; }
        [Required, MaxLength(100)] public string VideoCodec { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string VideoBitrate { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string VideoBitDepth { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string VideoFrameRate { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string VideoResolution { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string AudioCodec { get; set; } = String.Empty;
        [Required, MaxLength(100)] public string AudioBitrate { get; set; } = String.Empty;
        [Required] public int Duration { get; set; } = 0;
        [Required] public int MyListId { get; set; } = 0;

        public virtual List<VideoLocalPlace> VideoLocalPlaces { get; set; }
    }
}
