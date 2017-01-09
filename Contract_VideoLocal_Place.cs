using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public class Contract_VideoLocal_Place
    {
        public int VideoLocal_Place_ID { get; set; }
        public int VideoLocalID { get; set; }
        public string FilePath { get; set; }
        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }

        public ImportFolder ImportFolder { get; set; }

    }
}
