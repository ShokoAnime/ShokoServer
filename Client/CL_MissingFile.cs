using Shoko.Models.Client;

namespace Shoko.Models.Client
{
    public class CL_MissingFile
    {
        public int EpisodeID { get; set; }
        public int FileID { get; set; }
        public int AnimeID { get; set; }
        public string AnimeTitle { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }

        public Client.CL_AnimeSeries_User AnimeSeries { get; set; }
    }
}