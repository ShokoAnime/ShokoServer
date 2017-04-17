using System;

namespace Shoko.Models.Server
{
    public class AnimeEpisode
    {
        #region Server DB columns

        public int AnimeEpisodeID { get; set; }
        public int AnimeSeriesID { get; set; }
        public int AniDB_EpisodeID { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }

        #endregion

    }
}