using System;

namespace JMMServerModels.DB
{
    public class FileNameHash
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Hash { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}
