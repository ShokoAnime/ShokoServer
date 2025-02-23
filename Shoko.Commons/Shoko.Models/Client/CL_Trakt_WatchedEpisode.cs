using System;

namespace Shoko.Models.Client
{
    public class CL_Trakt_WatchedEpisode 
    {
        public int Trakt_EpisodeID { get; set; }

        public int Watched { get; set; }
        public DateTime? WatchedDate { get; set; }

        public string Episode_Season { get; set; }
        public string Episode_Number { get; set; }
        public string Episode_Title { get; set; }
        public string Episode_Overview { get; set; }
        public string Episode_Url { get; set; }
        public string Episode_Screenshot { get; set; }

        public CL_TraktTVShowResponse TraktShow { get; set; }

        public int? AnimeSeriesID { get; set; }
        public CL_AniDB_Anime Anime { get; set; }
    }
}