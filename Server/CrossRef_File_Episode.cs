using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Server
{
    public class CrossRef_File_Episode
    {
        public int CrossRef_File_EpisodeID { get; private set; }
        public string Hash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int CrossRefSource { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
    }
}
