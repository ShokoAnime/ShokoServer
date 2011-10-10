using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Entities
{
	public class AnimeSeries_User
	{
		public int AnimeSeries_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeSeriesID { get; set; }

		public int UnwatchedEpisodeCount { get; set; }
		public int WatchedEpisodeCount { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }

		public AnimeSeries_User()
		{
		}

		public AnimeSeries_User(int userID, int seriesID)
		{
			JMMUserID = userID;
			AnimeSeriesID = seriesID;

			UnwatchedEpisodeCount = 0;
			WatchedEpisodeCount = 0;
			WatchedDate = null;
			PlayedCount = 0;
			WatchedCount = 0;
			StoppedCount = 0;
		}
	}
}
