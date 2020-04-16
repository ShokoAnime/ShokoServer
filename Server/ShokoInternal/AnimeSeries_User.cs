using System;

namespace Shoko.Models.Server
{
    public class AnimeSeries_User : ICloneable
    {
        #region DB Columns

        public int AnimeSeries_UserID { get; set; }
        public int JMMUserID { get; set; }
        public int AnimeSeriesID { get; set; }

        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }


        #endregion


        public object Clone()
        {
            return new AnimeSeries_User
            {
                AnimeSeries_UserID = AnimeSeries_UserID,
                JMMUserID = JMMUserID,
                AnimeSeriesID = AnimeSeriesID,
                UnwatchedEpisodeCount = UnwatchedEpisodeCount,
                WatchedEpisodeCount = WatchedEpisodeCount,
                WatchedDate = WatchedDate,
                PlayedCount = PlayedCount,
                WatchedCount = WatchedCount,
                StoppedCount = StoppedCount
            };
        }
    }
}
