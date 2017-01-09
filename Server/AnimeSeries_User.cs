using System;
using System.Collections.Generic;
using Shoko.Models.PlexAndKodi;

using Shoko.Models;
using Shoko.Models.Enums;

namespace Shoko.Models.Server
{
    public class AnimeSeries_User
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


    }
}