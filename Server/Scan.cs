using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Server
{
    public class Scan
    {
        public int ScanID { get; private set; }
        public DateTime CreationTIme { get; set; }
        public string ImportFolders { get; set; }
        public int Status { get; set; }
    }
}
