using System;

namespace Shoko.Models
{
    public class Contract_Trakt_WatchedEpisode
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

        public Contract_TraktTVShowResponse TraktShow { get; set; }

        public int? AnimeSeriesID { get; set; }
        public Contract_AniDBAnime Anime { get; set; }
    }
}