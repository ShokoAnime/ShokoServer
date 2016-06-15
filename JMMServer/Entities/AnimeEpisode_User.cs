using System;

namespace JMMServer.Entities
{
    public class AnimeEpisode_User
    {
        public int AnimeEpisode_UserID { get; private set; }
        public int JMMUserID { get; set; }
        public int AnimeEpisodeID { get; set; }
        public int AnimeSeriesID { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }
    }
}