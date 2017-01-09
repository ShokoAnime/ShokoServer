using System;

namespace Shoko.Models
{
    public class Contract_AniDB_Episode
    {
        public int AniDB_EpisodeID { get; set; }
        public int EpisodeID { get; set; }
        public int AnimeID { get; set; }
        public int LengthSeconds { get; set; }
        public string Rating { get; set; }
        public string Votes { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }
        public string RomajiName { get; set; }
        public string EnglishName { get; set; }
        public int AirDate { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}