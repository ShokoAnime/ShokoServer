using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_File_Episode
    {
        public int CrossRef_File_EpisodeID { get; set; }
        public string Hash { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
        public string Username { get; set; }
        public long DateTimeUpdated { get; set; }

        public CrossRef_File_Episode()
		{
		}
    }
}
