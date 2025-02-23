namespace Shoko.Models.Client
{
    public class CL_MissingEpisode
    {
        public int EpisodeID { get; set; }
        public int AnimeID { get; set; }
        public string AnimeTitle { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }
        public string GroupFileSummary { get; set; }
        public string GroupFileSummarySimple { get; set; }

        public CL_AnimeSeries_User AnimeSeries { get; set; }
    }
}