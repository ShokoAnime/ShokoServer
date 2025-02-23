using System;

namespace Shoko.Models.Server
{
    public class FileNameHash
    {
        public int FileNameHashID { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Hash { get; set; }
        public DateTime DateTimeUpdated { get; set; }

    }
}