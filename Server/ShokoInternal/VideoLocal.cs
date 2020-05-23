using System;

namespace Shoko.Models.Server
{
    public class VideoLocal 
    {
        #region Server DB columns
        public int VideoLocalID { get; set; }
        public string Hash { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public int HashSource { get; set; }
        public long FileSize { get; set; }
        public int IsIgnored { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public int IsVariation { get; set; }

        [Obsolete("Use VideoLocal_Place.FilePath instead")]
        public string FileName { get; set; }
        
        #endregion
    }
}
