using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Server
{
    public class DuplicateFile
    {
        public int DuplicateFileID { get; set; }
        public string FilePathFile1 { get; set; }
        public string FilePathFile2 { get; set; }
        public string Hash { get; set; }
        public int ImportFolderIDFile1 { get; set; }
        public int ImportFolderIDFile2 { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}
