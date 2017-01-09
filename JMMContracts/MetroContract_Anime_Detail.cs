using System;
using System.Collections.Generic;

namespace Shoko.Models
{
    public class MetroContract_Anime_Detail
    {
        // anime details
        public int AnimeID { get; set; }
        public int AnimeSeriesID { get; set; }
        public string AnimeName { get; set; }
        public string AnimeType { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public int PosterImageType { get; set; }
        public int PosterImageID { get; set; }
        public int FanartImageType { get; set; }
        public int FanartImageID { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string AllTags { get; set; }
        public decimal OverallRating { get; set; }
        public int TotalVotes { get; set; }

        // next episode details
        public List<MetroContract_Anime_Episode> NextEpisodesToWatch { get; set; }
    }
}