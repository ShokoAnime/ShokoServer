using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_GroupStatus
	{
		public int AniDB_GroupStatusID { get; private set; }
		public int AnimeID { get; set; }
		public int GroupID { get; set; }
		public string GroupName { get; set; }
		public int CompletionState { get; set; }
		public int LastEpisodeNumber { get; set; }
		public int Rating { get; set; }
		public int Votes { get; set; }
		public string EpisodeRange { get; set; }

		public AniDB_GroupStatus()
		{
		}

		public AniDB_GroupStatus(Raw_AniDB_GroupStatus raw)
		{
			this.AnimeID = raw.AnimeID;
			this.GroupID = raw.GroupID;
			this.GroupName = raw.GroupName;
			this.CompletionState = raw.CompletionState;
			this.LastEpisodeNumber = raw.LastEpisodeNumber;
			this.Rating = raw.Rating;
			this.Votes = raw.Votes;
			this.EpisodeRange = raw.EpisodeRange;
		}

		/// <summary>
		/// looking at the episode range determine if the group has released a file
		/// for the specified episode number
		/// </summary>
		/// <param name="episodeNumber"></param>
		/// <returns></returns>
		public bool HasGroupReleasedEpisode(int episodeNumber)
		{
			// examples
			// 1-12
			// 1
			// 5-10
			// 1-10, 12

			string[] ranges = EpisodeRange.Split(',');

			foreach (string range in ranges)
			{
				string[] subRanges = range.Split('-');
				if (subRanges.Length == 1) // 1 episode
				{
					if (int.Parse(subRanges[0]) == episodeNumber) return true;
				}
				if (subRanges.Length == 2) // range
				{
					if (episodeNumber >= int.Parse(subRanges[0]) && episodeNumber <= int.Parse(subRanges[1])) return true;
				}
			}

			return false;
		}

		public override string ToString()
		{
			return string.Format("Group Status for anime: {0} - {1} : {2}", AnimeID, GroupName, EpisodeRange);
		}
	}
}
