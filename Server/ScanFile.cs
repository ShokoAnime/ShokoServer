using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Server
{
    public class ScanFile
    {
        public int ScanFileID { get; set; }
        public int ScanID { get; set; }
        public int ImportFolderID { get; set; }
        public int VideoLocal_Place_ID { get; set; }
        public string FullName { get; set; }
        public long FileSize { get; set; }
        public int Status { get; set; }
        public DateTime? CheckDate { get; set; }
        public string Hash { get; set; }
        public string HashResult { get; set; }

        public ScanFile()
        {
            
        }
    }
}
