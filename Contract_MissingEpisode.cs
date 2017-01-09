using Shoko.Models.Client;

namespace Shoko.Models
{
    public class Contract_MissingEpisode
    {
        public int EpisodeID { get; set; }
        public int AnimeID { get; set; }
        public string AnimeTitle { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }
        public string GroupFileSummary { get; set; }
        public string GroupFileSummarySimple { get; set; }

        public Client.CL_AnimeSeries_User AnimeSeries { get; set; }
    }
}