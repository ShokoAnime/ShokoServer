using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace JMMServer.Entities
{
	public class AnimeGroup_User
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		#region DB Columns
		public int AnimeGroup_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeGroupID { get; set; }
		public int IsFave { get; set; }
		public int UnwatchedEpisodeCount { get; set; }
		public int WatchedEpisodeCount { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }
		#endregion

		public AnimeGroup_User()
		{
		}

		public AnimeGroup_User(int userID, int groupID)
		{
			JMMUserID = userID;
			AnimeGroupID = groupID;

			IsFave = 0;
			UnwatchedEpisodeCount = 0;
			WatchedEpisodeCount = 0;
			WatchedDate = null;
			PlayedCount = 0;
			WatchedCount = 0;
			StoppedCount = 0;
		}

		

		public bool HasUnwatchedFiles
		{
			get
			{
				return UnwatchedEpisodeCount > 0;
			}
		}

		public bool AllFilesWatched
		{
			get
			{
				return UnwatchedEpisodeCount == 0;
			}
		}

		public bool AnyFilesWatched
		{
			get
			{
				return WatchedEpisodeCount > 0;
			}
		}
	}
}
