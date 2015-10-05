using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class ImportFolder
    {
        public string Id { get; set; }
        public ImportFolderType Type { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public bool IsDropSource { get; set; }
        public bool IsDropDestination { get; set; }
        public bool IsWatched { get; set; }
    }
}
