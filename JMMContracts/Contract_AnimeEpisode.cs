using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AnimeEpisode
	{
		// from AnimeEpisode
		public int AnimeEpisodeID { get; set; }
		public int EpisodeNumber { get; set; }
		public string EpisodeNameRomaji { get; set; }
		public string EpisodeNameEnglish { get; set; }
		public int EpisodeType { get; set; }
		public int AnimeSeriesID { get; set; }
		public int AniDB_EpisodeID { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public int IsWatched { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }
		public int LocalFileCount { get; set; }
		public int UnwatchedEpCountSeries { get; set; }

		// from AniDB_Episode
		public int AniDB_LengthSeconds { get; set; }
		public string AniDB_Rating { get; set; }
		public string AniDB_Votes { get; set; }
		public string AniDB_RomajiName { get; set; }
		public string AniDB_EnglishName { get; set; }
		public DateTime? AniDB_AirDate { get; set; }

		public List<Contract_AniDBReleaseGroup> ReleaseGroups { get; set; }
	}
}
