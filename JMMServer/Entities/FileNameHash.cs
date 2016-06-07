using System;

namespace JMMServer.Entities
{
    public class FileNameHash
    {
        public int FileNameHashID { get; private set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Hash { get; set; }
        public DateTime DateTimeUpdated { get; set; }

        public void Populate(CrossRef_File_Episode cfe)
        {
            FileName = cfe.FileName;
            FileSize = cfe.FileSize;
            Hash = cfe.Hash;
            DateTimeUpdated = DateTime.Now;
        }
    }
}