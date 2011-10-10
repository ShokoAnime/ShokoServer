using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Repositories;
using JMMContracts;
using System.Xml.Serialization;
using BinaryNorthwest;

namespace JMMServer.Entities
{
	public class AnimeGroup
	{
		#region DB Columns
		public int AnimeGroupID { get; private set; }
		public int? AnimeGroupParentID { get; set; }
		public string GroupName { get; set; }
		public string Description { get; set; }
		public int IsManuallyNamed { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public DateTime DateTimeCreated { get; set; }
		public string SortName { get; set; }
		public DateTime? EpisodeAddedDate { get; set; }
		public int MissingEpisodeCount { get; set; }
		public int MissingEpisodeCountGroups { get; set; }
		public int OverrideDescription { get; set; }
		#endregion

		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(int animeid)
		{
			// TODO we need to recusrive list at all relations and not just the first one
			AniDB_AnimeRepository repAniAnime = new AniDB_AnimeRepository();
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AnimeGroupRepository repGroups = new AnimeGroupRepository();

			List<AnimeGroup> grps = new List<AnimeGroup>();

			AniDB_Anime anime = repAniAnime.GetByAnimeID(animeid);
			if (anime == null) return grps;

			// first check for groups which are directly related
			List<AniDB_Anime_Relation> relations = anime.RelatedAnime;
			foreach (AniDB_Anime_Relation rel in relations)
			{
				// we actually need to get the series, because it might have been added to another group already
				AnimeSeries ser = repSeries.GetByAnimeID(rel.RelatedAnimeID);
				if (ser != null)
				{
					AnimeGroup grp = repGroups.GetByID(ser.AnimeGroupID);
					if (grp != null) grps.Add(grp);
				}
			}
			if (grps.Count > 0) return grps;

			// if nothing found check by all related anime
			List<AniDB_Anime> relatedAnime = anime.AllRelatedAnime;
			foreach (AniDB_Anime rel in relatedAnime)
			{
				// we actually need to get the series, because it might have been added to another group already
				AnimeSeries ser = repSeries.GetByAnimeID(rel.AnimeID);
				if (ser != null)
				{
					AnimeGroup grp = repGroups.GetByID(ser.AnimeGroupID);
					if (grp != null) grps.Add(grp);
				}
			}

			return grps;
		}

		public AnimeGroup_User GetUserRecord(int userID)
		{
			AnimeGroup_UserRepository repUser = new AnimeGroup_UserRepository();
			return repUser.GetByUserAndGroupID(userID, this.AnimeGroupID);
		}

		public void Populate(AnimeSeries series)
		{
			this.Description = series.Anime.Description;
			this.GroupName = series.Anime.PreferredTitle;
			this.SortName = series.Anime.PreferredTitle;
			this.DateTimeUpdated = DateTime.Now;
			this.DateTimeCreated = DateTime.Now;
		}

		public bool HasMissingEpisodesAny
		{
			get
			{
				return (MissingEpisodeCount > 0 || MissingEpisodeCountGroups > 0);
			}
		}

		public bool HasMissingEpisodesGroups
		{
			get
			{
				return MissingEpisodeCountGroups > 0;
			}
		}

		public bool HasMissingEpisodes
		{
			get
			{
				return MissingEpisodeCountGroups > 0;
			}
		}

		public List<string> AnimeTypesList
		{
			get
			{
				List<string> atypeList = new List<string>();
				foreach (AnimeSeries series in AllSeries)
				{
					string atype = series.Anime.AnimeTypeDescription;
					if (!atypeList.Contains(atype)) atypeList.Add(atype);
				}
				return atypeList;
			}
		}

		/// <summary>
		/// Renames all Anime groups based on the user's language preferences
		/// </summary>
		public static void RenameAllGroups()
		{
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			List<AnimeGroup> groupsToSave = new List<AnimeGroup>();
			foreach (AnimeGroup grp in repGroups.GetAll())
			{
				// only rename the group if it has one direct child Anime Series
				if (grp.Series.Count == 1)
				{
					string newTitle = grp.Series[0].Anime.PreferredTitle;
					grp.GroupName = newTitle;
					grp.SortName = newTitle;
					groupsToSave.Add(grp);
					repGroups.Save(grp);
				}
			}

			foreach (AnimeGroup grp in groupsToSave)
				repGroups.Save(grp);
		}

		public List<AniDB_Anime> Anime
		{
			get
			{
				List<AniDB_Anime> relAnime = new List<AniDB_Anime>();
				foreach (AnimeSeries serie in Series)
				{
					AniDB_Anime anime = serie.Anime;
					if (anime != null) relAnime.Add(anime);

				}
				return relAnime;
			}
		}

		public Contract_AnimeGroup ToContract(AnimeGroup_User userRecord)
		{
			Contract_AnimeGroup contract = new Contract_AnimeGroup();
			contract.AnimeGroupID = this.AnimeGroupID;
			contract.AnimeGroupParentID = this.AnimeGroupParentID;
			contract.GroupName = this.GroupName;
			contract.Description = this.Description;
			contract.SortName = this.SortName;
			contract.EpisodeAddedDate = this.EpisodeAddedDate;
			contract.OverrideDescription = this.OverrideDescription;
			contract.DateTimeUpdated = this.DateTimeUpdated;

			if (userRecord == null)
			{
				contract.IsFave = 0;
				contract.UnwatchedEpisodeCount = 0;
				contract.WatchedEpisodeCount = 0;
				contract.WatchedDate = null;
				contract.PlayedCount = 0;
				contract.WatchedCount = 0;
				contract.StoppedCount = 0;
			}
			else
			{
				contract.IsFave = userRecord.IsFave;
				contract.UnwatchedEpisodeCount = userRecord.UnwatchedEpisodeCount;
				contract.WatchedEpisodeCount = userRecord.WatchedEpisodeCount;
				contract.WatchedDate = userRecord.WatchedDate;
				contract.PlayedCount = userRecord.PlayedCount;
				contract.WatchedCount = userRecord.WatchedCount;
				contract.StoppedCount = userRecord.StoppedCount;
			}

			contract.MissingEpisodeCount = this.MissingEpisodeCount;
			contract.MissingEpisodeCountGroups = this.MissingEpisodeCountGroups;

			if (StatsCache.Instance.StatGroupAudioLanguages.ContainsKey(this.AnimeGroupID))
				contract.Stat_AudioLanguages = StatsCache.Instance.StatGroupAudioLanguages[this.AnimeGroupID];
			else contract.Stat_AudioLanguages = "";

			if (StatsCache.Instance.StatGroupSubtitleLanguages.ContainsKey(this.AnimeGroupID))
				contract.Stat_SubtitleLanguages = StatsCache.Instance.StatGroupSubtitleLanguages[this.AnimeGroupID];
			else contract.Stat_SubtitleLanguages = "";

			if (StatsCache.Instance.StatGroupVideoQuality.ContainsKey(this.AnimeGroupID))
				contract.Stat_AllVideoQuality = StatsCache.Instance.StatGroupVideoQuality[this.AnimeGroupID];
			else contract.Stat_AllVideoQuality = "";

			if (StatsCache.Instance.StatGroupVideoQualityEpisodes.ContainsKey(this.AnimeGroupID))
				contract.Stat_AllVideoQuality_Episodes = StatsCache.Instance.StatGroupVideoQualityEpisodes[this.AnimeGroupID];
			else contract.Stat_AllVideoQuality_Episodes = "";

			if (StatsCache.Instance.StatGroupIsComplete.ContainsKey(this.AnimeGroupID))
				contract.Stat_IsComplete = StatsCache.Instance.StatGroupIsComplete[this.AnimeGroupID];
			else contract.Stat_IsComplete = false;

			if (StatsCache.Instance.StatGroupHasTvDB.ContainsKey(this.AnimeGroupID))
				contract.Stat_HasTvDBLink = StatsCache.Instance.StatGroupHasTvDB[this.AnimeGroupID];
			else contract.Stat_HasTvDBLink = false;

			if (StatsCache.Instance.StatGroupHasMovieDB.ContainsKey(this.AnimeGroupID))
				contract.Stat_HasMovieDBLink = StatsCache.Instance.StatGroupHasMovieDB[this.AnimeGroupID];
			else contract.Stat_HasMovieDBLink = false;

			if (StatsCache.Instance.StatGroupHasMovieDBOrTvDB.ContainsKey(this.AnimeGroupID))
				contract.Stat_HasMovieDBOrTvDBLink = StatsCache.Instance.StatGroupHasMovieDBOrTvDB[this.AnimeGroupID];
			else contract.Stat_HasMovieDBOrTvDBLink = false;

			if (StatsCache.Instance.StatGroupIsFinishedAiring.ContainsKey(this.AnimeGroupID))
				contract.Stat_HasFinishedAiring = StatsCache.Instance.StatGroupIsFinishedAiring[this.AnimeGroupID];
			else contract.Stat_HasFinishedAiring = false;

			if (StatsCache.Instance.StatGroupAirDate_Max.ContainsKey(this.AnimeGroupID))
				contract.Stat_AirDate_Max = StatsCache.Instance.StatGroupAirDate_Max[this.AnimeGroupID];
			else contract.Stat_AirDate_Max = null;

			if (StatsCache.Instance.StatGroupAirDate_Min.ContainsKey(this.AnimeGroupID))
				contract.Stat_AirDate_Min = StatsCache.Instance.StatGroupAirDate_Min[this.AnimeGroupID];
			else contract.Stat_AirDate_Min = null;

			if (StatsCache.Instance.StatGroupCategories.ContainsKey(this.AnimeGroupID))
				contract.Stat_AllCategories = StatsCache.Instance.StatGroupCategories[this.AnimeGroupID];
			else contract.Stat_AllCategories = "";

			if (StatsCache.Instance.StatGroupEndDate.ContainsKey(this.AnimeGroupID))
				contract.Stat_EndDate = StatsCache.Instance.StatGroupEndDate[this.AnimeGroupID];
			else contract.Stat_EndDate = null;

			if (StatsCache.Instance.StatGroupSeriesCreatedDate.ContainsKey(this.AnimeGroupID))
				contract.Stat_SeriesCreatedDate = StatsCache.Instance.StatGroupSeriesCreatedDate[this.AnimeGroupID];
			else contract.Stat_SeriesCreatedDate = null;

			if (StatsCache.Instance.StatGroupTitles.ContainsKey(this.AnimeGroupID))
				contract.Stat_AllTitles = StatsCache.Instance.StatGroupTitles[this.AnimeGroupID];
			else contract.Stat_AllTitles = "";

			if (StatsCache.Instance.StatGroupUserVoteOverall.ContainsKey(this.AnimeGroupID))
				contract.Stat_UserVoteOverall = StatsCache.Instance.StatGroupUserVoteOverall[this.AnimeGroupID];
			else contract.Stat_UserVoteOverall = null;

			if (StatsCache.Instance.StatGroupUserVotePermanent.ContainsKey(this.AnimeGroupID))
				contract.Stat_UserVotePermanent = StatsCache.Instance.StatGroupUserVotePermanent[this.AnimeGroupID];
			else contract.Stat_UserVotePermanent = null;

			if (StatsCache.Instance.StatGroupUserVoteTemporary.ContainsKey(this.AnimeGroupID))
				contract.Stat_UserVoteTemporary = StatsCache.Instance.StatGroupUserVoteTemporary[this.AnimeGroupID];
			else contract.Stat_UserVoteTemporary = null;

			if (StatsCache.Instance.StatGroupSeriesCount.ContainsKey(this.AnimeGroupID))
				contract.Stat_SeriesCount = StatsCache.Instance.StatGroupSeriesCount[this.AnimeGroupID];
			else contract.Stat_SeriesCount = 0;

			//contract.AniDB_AirDate = this.AirDate;
			//contract.AniDB_Year = animeRec.Year;

			return contract;
		}

		public decimal AniDBRating
		{
			get
			{
				try
				{
					decimal totalRating = 0;
					int totalVotes = 0;

					foreach (AniDB_Anime anime in Anime)
					{
						totalRating += (decimal)anime.AniDBTotalRating;
						totalVotes += anime.AniDBTotalVotes;
					}

					if (totalVotes == 0)
						return 0;
					else
						return totalRating / (decimal)totalVotes;

				}
				catch (Exception ex)
				{
					logger.Error("Error in  AniDBRating: {0}", ex.ToString());
					return 0;
				}
			}
		}

		[XmlIgnore]
		public List<AnimeGroup> ChildGroups
		{
			get
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				return repGroups.GetByParentID(this.AnimeGroupID);
			}
		}

		[XmlIgnore]
		public List<AnimeGroup> AllChildGroups
		{
			get
			{
				List<AnimeGroup> grpList = new List<AnimeGroup>();
				AnimeGroup.GetAnimeGroupsRecursive(this.AnimeGroupID, ref grpList);
				return grpList;
			}
		}

		[XmlIgnore]
		public List<AnimeSeries> Series
		{
			get
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				List<AnimeSeries> seriesList = repSeries.GetByGroupID(this.AnimeGroupID);

				/*List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("Year", false, SortType.eString));
				seriesList = Sorting.MultiSort<AnimeSeries>(seriesList, sortCriteria);*/

				return seriesList;
			}
		}

		[XmlIgnore]
		public List<AnimeSeries> AllSeries
		{
			get
			{
				List<AnimeSeries> seriesList = new List<AnimeSeries>();
				AnimeGroup.GetAnimeSeriesRecursive(this.AnimeGroupID, ref seriesList);

				/*List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("Year", false, SortType.eString));
				seriesList = Sorting.MultiSort<AnimeSeries>(seriesList, sortCriteria);*/

				return seriesList;
			}
		}

		public string CategoriesString
		{
			get
			{
				string temp = "";
				foreach (AniDB_Category cat in Categories)
					temp += cat.CategoryName + "|";
				if (temp.Length > 2)
					temp = temp.Substring(0, temp.Length - 2);

				return temp;
			}
		}

		public List<AniDB_Category> Categories
		{
			get
			{
				List<AniDB_Category> cats = new List<AniDB_Category>();
				List<int> animeCatIDs = new List<int>();
				List<AniDB_Anime_Category> animeCats = new List<AniDB_Anime_Category>();

				// get a list of all the unique categories for this all the series in this group
				foreach (AnimeSeries ser in AllSeries)
				{
					foreach (AniDB_Anime_Category aac in ser.Anime.AnimeCategories)
					{
						if (!animeCatIDs.Contains(aac.AniDB_Anime_CategoryID))
						{
							animeCatIDs.Add(aac.AniDB_Anime_CategoryID);
							animeCats.Add(aac);
						}
					}
				}

				// now sort it by the weighting
				List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("Weighting", true, SortType.eInteger));
				animeCats = Sorting.MultiSort<AniDB_Anime_Category>(animeCats, sortCriteria);

				AniDB_CategoryRepository repCat = new AniDB_CategoryRepository();
				foreach (AniDB_Anime_Category animeCat in animeCats)
				{
					AniDB_Category cat = repCat.GetByCategoryID(animeCat.CategoryID);
					if (cat != null) cats.Add(cat);
				}
				
				return cats;
			}
		}

		public List<AniDB_Anime_Title> Titles
		{
			get
			{
				List<int> animeTitleIDs = new List<int>();
				List<AniDB_Anime_Title> animeTitles = new List<AniDB_Anime_Title>();


				// get a list of all the unique titles for this all the series in this group
				foreach (AnimeSeries ser in AllSeries)
				{
					foreach (AniDB_Anime_Title aat in ser.Anime.Titles)
					{
						if (!animeTitleIDs.Contains(aat.AniDB_Anime_TitleID))
						{
							animeTitleIDs.Add(aat.AniDB_Anime_TitleID);
							animeTitles.Add(aat);
						}
					}
				}

				return animeTitles;
			}
		}

		public string TitlesString
		{
			get
			{
				string temp = "";
				foreach (AniDB_Anime_Title title in Titles)
					temp += title.Title + ", ";
				if (temp.Length > 2)
					temp = temp.Substring(0, temp.Length - 2);

				return temp;
			}
		}

		public string VideoQualityString
		{
			get
			{
				AdhocRepository rep = new AdhocRepository();
				return rep.GetAllVideoQualityForGroup(this.AnimeGroupID);
			}
		}

		public decimal? UserVote
		{
			get
			{
				decimal totalVotes = 0;
				int countVotes = 0;
				foreach (AnimeSeries ser in AllSeries)
				{
					AniDB_Vote vote = ser.Anime.UserVote;
					if (vote != null)
					{
						countVotes++;
						totalVotes += (decimal)vote.VoteValue;
					}
				}

				if (countVotes == 0)
					return null;
				else
					return totalVotes / (decimal)countVotes / (decimal)100;
				

			}
		}

		public decimal? UserVotePermanent
		{
			get
			{
				decimal totalVotes = 0;
				int countVotes = 0;
				foreach (AnimeSeries ser in AllSeries)
				{
					AniDB_Vote vote = ser.Anime.UserVote;
					if (vote != null && vote.VoteType == (int)AniDBVoteType.Anime)
					{
						countVotes++;
						totalVotes += (decimal)vote.VoteValue;
					}
				}

				if (countVotes == 0)
					return null;
				else
					return totalVotes / (decimal)countVotes / (decimal)100;

			}
		}

		public decimal? UserVoteTemporary
		{
			get
			{
				decimal totalVotes = 0;
				int countVotes = 0;
				foreach (AnimeSeries ser in AllSeries)
				{
					AniDB_Vote vote = ser.Anime.UserVote;
					if (vote != null && vote.VoteType == (int)AniDBVoteType.AnimeTemp)
					{
						countVotes++;
						totalVotes += (decimal)vote.VoteValue;
					}
				}

				if (countVotes == 0)
					return null;
				else
					return totalVotes / (decimal)countVotes / (decimal)100;

			}
		}

		public override string ToString()
		{
			return string.Format("Group: {0} ({1})", GroupName, AnimeGroupID);
			//return "";
		}

		public void UpdateStatsFromTopLevel(bool watchedStats, bool missingEpsStats)
		{
			UpdateStatsFromTopLevel(false, watchedStats, missingEpsStats);
		}

		/// <summary>
		/// Update stats for all child groups and series
		/// This should only be called from the very top level group.
		/// </summary>
		public void UpdateStatsFromTopLevel(bool updateGroupStatsOnly, bool watchedStats, bool missingEpsStats)
		{
			if (this.AnimeGroupParentID.HasValue) return;

			// update the stats for all the sries first
			if (!updateGroupStatsOnly)
			{
				foreach (AnimeSeries ser in AllSeries)
				{
					ser.UpdateStats(watchedStats, missingEpsStats, false);
				}
			}

			// now recursively update stats for all the child groups
			// and update the stats for the groups
			foreach (AnimeGroup grp in AllChildGroups)
			{
				grp.UpdateStats(watchedStats, missingEpsStats);
			}

			UpdateStats(watchedStats, missingEpsStats);
		}

		/// <summary>
		/// Update the stats for this group based on the child series
		/// Assumes that all the AnimeSeries have had their stats updated already
		/// </summary>
		public void UpdateStats(bool watchedStats, bool missingEpsStats)
		{
			List<AnimeSeries> seriesList = AllSeries;

			JMMUserRepository repUsers = new JMMUserRepository();
			List<JMMUser> allUsers = repUsers.GetAll();

			if (watchedStats)
			{
				foreach (JMMUser juser in allUsers)
				{
					AnimeGroup_User userRecord = GetUserRecord(juser.JMMUserID);
					if (userRecord == null) userRecord = new AnimeGroup_User(juser.JMMUserID, this.AnimeGroupID);
	
					// reset stats
					userRecord.WatchedCount = 0;
					userRecord.UnwatchedEpisodeCount = 0;
					userRecord.PlayedCount = 0;
					userRecord.StoppedCount = 0;
					userRecord.WatchedEpisodeCount = 0;
					userRecord.WatchedDate = null;

					foreach (AnimeSeries ser in seriesList)
					{
						AnimeSeries_User serUserRecord = ser.GetUserRecord(juser.JMMUserID);
						if (serUserRecord != null)
						{
							userRecord.WatchedCount += serUserRecord.WatchedCount;
							userRecord.UnwatchedEpisodeCount += serUserRecord.UnwatchedEpisodeCount;
							userRecord.PlayedCount += serUserRecord.PlayedCount;
							userRecord.StoppedCount += serUserRecord.StoppedCount;
							userRecord.WatchedEpisodeCount += serUserRecord.WatchedEpisodeCount;

							if (serUserRecord.WatchedDate.HasValue)
							{
								if (userRecord.WatchedDate.HasValue)
								{
									if (serUserRecord.WatchedDate > userRecord.WatchedDate)
										userRecord.WatchedDate = serUserRecord.WatchedDate;
								}
								else
									userRecord.WatchedDate = serUserRecord.WatchedDate;
							}
						}
					}

					// now update the stats for the groups
					logger.Trace("Updating stats for {0}", this.ToString());
					AnimeGroup_UserRepository rep = new AnimeGroup_UserRepository();
					rep.Save(userRecord);
				}
			}

			if (missingEpsStats)
			{
				this.MissingEpisodeCount = 0;
				this.MissingEpisodeCountGroups = 0;

				foreach (AnimeSeries ser in seriesList)
				{
					this.MissingEpisodeCount += ser.MissingEpisodeCount;
					this.MissingEpisodeCountGroups += ser.MissingEpisodeCountGroups;
				}

				AnimeGroupRepository repGrp = new AnimeGroupRepository();
				repGrp.Save(this);
			}

			
		}

		public static void GetAnimeGroupsRecursive(int animeGroupID, ref List<AnimeGroup> groupList)
		{
			AnimeGroupRepository rep = new AnimeGroupRepository();
			AnimeGroup grp = rep.GetByID(animeGroupID);
			if (grp == null) return;

			// get the child groups for this group
			groupList.AddRange(grp.ChildGroups);

			foreach (AnimeGroup childGroup in grp.ChildGroups)
			{
				GetAnimeGroupsRecursive(childGroup.AnimeGroupID, ref groupList);
			}
		}


		public static void GetAnimeSeriesRecursive(int animeGroupID, ref List<AnimeSeries> seriesList)
		{
			AnimeGroupRepository rep = new AnimeGroupRepository();
			AnimeGroup grp = rep.GetByID(animeGroupID);
			if (grp == null) return;

			// get the series for this group
			List<AnimeSeries> thisSeries = grp.Series;
			seriesList.AddRange(thisSeries);

			foreach (AnimeGroup childGroup in grp.ChildGroups)
			{
				GetAnimeSeriesRecursive(childGroup.AnimeGroupID, ref seriesList);
			}
		}

		public AnimeGroup TopLevelAnimeGroup
		{
			get
			{
				if (!AnimeGroupParentID.HasValue) return this;
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup parentGroup = repGroups.GetByID(this.AnimeGroupParentID.Value);

				while (parentGroup.AnimeGroupParentID.HasValue)
				{
					parentGroup = repGroups.GetByID(parentGroup.AnimeGroupParentID.Value);
				}
				return parentGroup;
			}
		}

	}
}
