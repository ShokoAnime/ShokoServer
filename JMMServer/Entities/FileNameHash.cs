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

        public void Populate(SVR_CrossRef_File_Episode cfe)
        {
            this.FileName = cfe.FileName;
            this.FileSize = cfe.FileSize;
            this.Hash = cfe.Hash;
            DateTimeUpdated = DateTime.Now;
        }
    }
}