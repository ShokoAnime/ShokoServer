using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AnimeGroup : IComparable<Contract_AnimeGroup>
	{
		// Data from AnimeGroup
		public int AnimeGroupID { get; set; }
		public int? AnimeGroupParentID { get; set; }
		public int? DefaultAnimeSeriesID { get; set; }
		public string GroupName { get; set; }
		public string Description { get; set; }
		public int IsFave { get; set; }
		public int IsManuallyNamed { get; set; }
		public int UnwatchedEpisodeCount { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public int WatchedEpisodeCount { get; set; }
		public string SortName { get; set; }
		public DateTime? WatchedDate { get; set; }
		public DateTime? EpisodeAddedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }
		public int OverrideDescription { get; set; }

		public int MissingEpisodeCount { get; set; }
		public int MissingEpisodeCountGroups { get; set; }

		public DateTime? Stat_AirDate_Min { get; set; }
		public DateTime? Stat_AirDate_Max { get; set; }
		public DateTime? Stat_EndDate { get; set; }
		public DateTime? Stat_SeriesCreatedDate { get; set; }
		public decimal? Stat_UserVotePermanent { get; set; }
		public decimal? Stat_UserVoteTemporary { get; set; }
		public decimal? Stat_UserVoteOverall { get; set; }
		public string Stat_AllCategories { get; set; }
		public string Stat_AllTitles { get; set; }
		public bool Stat_IsComplete { get; set; }
		public bool Stat_HasFinishedAiring { get; set; }
		public bool Stat_HasTvDBLink { get; set; }
		public bool Stat_HasMALLink { get; set; }
		public bool Stat_HasMovieDBLink { get; set; }
		public bool Stat_HasMovieDBOrTvDBLink { get; set; }
		public string Stat_AllVideoQuality { get; set; }
		public string Stat_AllVideoQuality_Episodes { get; set; }
		public string Stat_AudioLanguages { get; set; }
		public string Stat_SubtitleLanguages { get; set; }
		public int Stat_SeriesCount { get; set; }
		public int Stat_EpisodeCount { get; set; }
		public decimal Stat_AniDBRating { get; set; }

		public int CompareTo(Contract_AnimeGroup obj)
		{
			return SortName.CompareTo(obj.SortName);
		}
	}
}
