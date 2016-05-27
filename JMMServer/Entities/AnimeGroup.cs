using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Repositories;
using System.Xml.Serialization;
using BinaryNorthwest;
using System.IO;
using FluentNHibernate.Utils;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.PlexAndKodi;
using NHibernate;

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
		public DateTime? LatestEpisodeAirDate { get; set; }
		public int MissingEpisodeCount { get; set; }
		public int MissingEpisodeCountGroups { get; set; }
		public int OverrideDescription { get; set; }
		public int? DefaultAnimeSeriesID { get; set; }

        public int ContractVersion { get; set; }
        public string ContractString { get; set; }


        #endregion


	    public const int CONTRACT_VERSION = 2;


        private static Logger logger = LogManager.GetCurrentClassLogger();


        internal Contract_AnimeGroup _contract = null;
        public virtual Contract_AnimeGroup Contract
        {
            get
            {
                if ((_contract == null) && (ContractVersion == CONTRACT_VERSION))
                    _contract = Newtonsoft.Json.JsonConvert.DeserializeObject<Contract_AnimeGroup>(ContractString);
                return _contract;
            }
            set
            {
                _contract = value;
                if (value != null)
                {
                    ContractVersion = CONTRACT_VERSION;
                    ContractString = Newtonsoft.Json.JsonConvert.SerializeObject(_contract);
                }
            }
        }     

        
        public string GetPosterPathNoBlanks()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetPosterPathNoBlanks(session);
			}
		}

		public string GetPosterPathNoBlanks(ISession session)
		{
			List<string> allPosters = GetPosterFilenames(session);
			string posterName = "";
			if (allPosters.Count > 0)
				//posterName = allPosters[fanartRandom.Next(0, allPosters.Count)];
				posterName = allPosters[0];

			if (!String.IsNullOrEmpty(posterName))
				return posterName;

			return "";
		}

		private List<string> GetPosterFilenames()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetPosterFilenames(session);
			}
		}

		private List<string> GetPosterFilenames(ISession session)
		{
			List<string> allPosters = new List<string>();

			// check if user has specied a fanart to always be used
			if (DefaultAnimeSeriesID.HasValue)
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries defaultSeries = repSeries.GetByID(session, DefaultAnimeSeriesID.Value);
				if (defaultSeries != null)
				{
					AniDB_Anime anime = defaultSeries.GetAnime(session);
					string defPosterPathNoBlanks = anime.GetDefaultPosterPathNoBlanks(session);

					if (!string.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
					{
						allPosters.Add(defPosterPathNoBlanks);
						return allPosters;
					}
				}
			}

			foreach (AnimeSeries ser in GetAllSeries(session))
			{
				AniDB_Anime anime = ser.GetAnime(session);
				string defPosterPathNoBlanks = anime.GetDefaultPosterPathNoBlanks(session);

				if (!string.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
					allPosters.Add(defPosterPathNoBlanks);
			}

			return allPosters;
		}

        public Contract_AnimeGroup GetUserContract(int userid)
        {
            if (Contract == null)
                return new Contract_AnimeGroup();
            Contract_AnimeGroup contract = (Contract_AnimeGroup)Contract.DeepCopy();
            AnimeGroup_User rr = GetUserRecord(userid);
            if (rr != null)
            {
                contract.IsFave = rr.IsFave;
                contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                contract.WatchedDate = rr.WatchedDate;
                contract.PlayedCount = rr.PlayedCount;
                contract.WatchedCount = rr.WatchedCount;
                contract.StoppedCount = rr.StoppedCount;
            }
            return contract;
        }


	    public Video GetPlexContract(int userid)
	    {
	        return GetOrCreateUserRecord(userid).PlexContract;
	    }

        private AnimeGroup_User GetOrCreateUserRecord(int userid)
	    {
            AnimeGroup_User rr = GetUserRecord(userid);
	        if (rr != null)
	            return rr;
            rr= new AnimeGroup_User(userid, this.AnimeGroupID);
            rr.WatchedCount = 0;
            rr.UnwatchedEpisodeCount = 0;
            rr.PlayedCount = 0;
            rr.StoppedCount = 0;
            rr.WatchedEpisodeCount = 0;
            rr.WatchedDate = null;
            AnimeGroup_UserRepository repo=new AnimeGroup_UserRepository();
            repo.Save(rr);
	        return rr;
	    }
		public static bool IsRelationTypeInExclusions(string type)
		{
			string[] list = ServerSettings.AutoGroupSeriesRelationExclusions.Split('|');
			foreach (string a in list)
			{
				if (a.ToLowerInvariant().Equals(type.ToLowerInvariant())) return true;
			}
			return false;
		}
		public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(int animeid, bool forceRecursive = false)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetRelatedGroupsFromAnimeID(session, animeid, forceRecursive);
			}
		}

		public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(ISession session, int animeid, bool forceRecursive = false)
		{
			AniDB_AnimeRepository repAniAnime = new AniDB_AnimeRepository();
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AnimeGroupRepository repGroups = new AnimeGroupRepository();

			List<AnimeGroup> grps = new List<AnimeGroup>();

			AniDB_Anime anime = repAniAnime.GetByAnimeID(session, animeid);
			if (anime == null) return grps;

			// first check for groups which are directly related
			List<AniDB_Anime_Relation> relations = anime.GetRelatedAnime(session);
			//TODO REMOVE sort, missing RelationCompare relations.Sort(RelationCompare);
			foreach (AniDB_Anime_Relation rel in relations)
			{

                string relationtype = rel.RelationType.ToLower();
				if (IsRelationTypeInExclusions(relationtype))
				{
                    //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                    continue;
                }

				// we actually need to get the series, because it might have been added to another group already
				AnimeSeries ser = repSeries.GetByAnimeID(session, rel.RelatedAnimeID);
				if (ser != null)
				{
					AnimeGroup grp = repGroups.GetByID(session, ser.AnimeGroupID);
					if (grp != null) grps.Add(grp);
				}
			}
			if (!forceRecursive && grps.Count > 0) return grps;

			// if nothing found check by all related anime
			List<AniDB_Anime> relatedAnime = anime.GetAllRelatedAnime(session);
			foreach (AniDB_Anime rel in relatedAnime)
			{
				// we actually need to get the series, because it might have been added to another group already
				AnimeSeries ser = repSeries.GetByAnimeID(session, rel.AnimeID);
				if (ser != null)
				{
					AnimeGroup grp = repGroups.GetByID(session, ser.AnimeGroupID);
					if (grp != null)
					{
						if (!grps.Contains(grp)) grps.Add(grp);
					}
				}
			}

			return grps;
		}

		public AnimeGroup_User GetUserRecord(int userID)
		{
			AnimeGroup_UserRepository repUser = new AnimeGroup_UserRepository();
			return repUser.GetByUserAndGroupID(userID, this.AnimeGroupID);
		}

		public AnimeGroup_User GetUserRecord(ISession session, int userID)
		{
			AnimeGroup_UserRepository repUser = new AnimeGroup_UserRepository();
			return repUser.GetByUserAndGroupID(session, userID, this.AnimeGroupID);
		}

		public void Populate(AnimeSeries series)
		{
			this.Description = series.GetAnime().Description;
			this.GroupName = series.GetAnime().PreferredTitle;
			this.SortName = series.GetAnime().PreferredTitle;
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
				foreach (AnimeSeries series in GetAllSeries())
				{
					string atype = series.GetAnime().AnimeTypeDescription;
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
			foreach (AnimeGroup grp in repGroups.GetAll().ToList())
			{
				List<AnimeSeries> list = grp.GetSeries();

				// only rename the group if it has one direct child Anime Series
				if (list.Count == 1)
				{
					string newTitle = list[0].GetAnime().PreferredTitle;
					grp.GroupName = newTitle;
					grp.SortName = newTitle;
					repGroups.Save(grp,true,false);
				}
				else if (list.Count > 1)
				{
					#region Naming
					AnimeSeries series = null;
					bool hasCustomName = true;
					if (grp.DefaultAnimeSeriesID.HasValue)
					{
						series = new AnimeSeriesRepository().GetByID(grp.DefaultAnimeSeriesID.Value);
						if (series == null)
						{
							grp.DefaultAnimeSeriesID = null;
						}
						else
						{
							hasCustomName = false;
						}
					}

					if (!grp.DefaultAnimeSeriesID.HasValue)
					{
						foreach (AnimeSeries ser in list)
						{
							if (ser == null) continue;
							if (series == null)
							{
								// Check all titles for custom naming, in case user changed language preferences
								if (ser.SeriesNameOverride.Equals(grp.GroupName))
								{
									hasCustomName = false;
								}
								else
								{
									foreach (AniDB_Anime_Title title in ser.GetAnime().GetTitles())
									{
										if (title.Title.Equals(grp.GroupName))
										{
											hasCustomName = false;
											break;
										}
									}
								}
								series = ser;
								continue;
							}
							if (ser.AirDate < series.AirDate) series = ser;
						}
					}
					if (series != null)
					{
						string newTitle = series.GetAnime().PreferredTitle;
						if (series.SeriesNameOverride != null && !series.SeriesNameOverride.Equals(""))
							newTitle = series.SeriesNameOverride;
						if (hasCustomName && (!grp.DefaultAnimeSeriesID.HasValue || series.AnimeSeriesID != grp.DefaultAnimeSeriesID.Value))
							newTitle = grp.GroupName;
						// reset tags, description, etc to new series
						grp.Populate(series);
						grp.GroupName = newTitle;
						grp.SortName = newTitle;
						repGroups.Save(grp,true,false);
					}
					#endregion
				}
			}
		}


		public List<AniDB_Anime> Anime
		{
			get
			{
				List<AniDB_Anime> relAnime = new List<AniDB_Anime>();
				foreach (AnimeSeries serie in GetSeries())
				{
					AniDB_Anime anime = serie.GetAnime();
					if (anime != null) relAnime.Add(anime);

				}
				return relAnime;
			}
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


		/*		[XmlIgnore]
		 public List<AnimeGroup> ChildGroups
		{
			get
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				return repGroups.GetByParentID(this.AnimeGroupID);
			}
		}*/

		public List<AnimeGroup> GetChildGroups()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetChildGroups(session);
			}
		}

		public List<AnimeGroup> GetChildGroups(ISession session)
		{
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			return repGroups.GetByParentID(session, this.AnimeGroupID);
		}

		/*[XmlIgnore]
		public List<AnimeGroup> AllChildGroups
		{
			get
			{
				List<AnimeGroup> grpList = new List<AnimeGroup>();
				AnimeGroup.GetAnimeGroupsRecursive(this.AnimeGroupID, ref grpList);
				return grpList;
			}
		}*/

		public List<AnimeGroup> GetAllChildGroups()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAllChildGroups(session);
			}
		}

		public List<AnimeGroup> GetAllChildGroups(ISession session)
		{
			List<AnimeGroup> grpList = new List<AnimeGroup>();
			AnimeGroup.GetAnimeGroupsRecursive(session, this.AnimeGroupID, ref grpList);
			return grpList;
		}

		/*[XmlIgnore]
		public List<AnimeSeries> Series
		{
			get
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				List<AnimeSeries> seriesList = repSeries.GetByGroupID(this.AnimeGroupID);

				return seriesList;
			}
		}*/

		public List<AnimeSeries> GetSeries()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetSeries(session);
			}
		}

		public List<AnimeSeries> GetSeries(ISession session)
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> seriesList = repSeries.GetByGroupID(this.AnimeGroupID);
			// Make everything that relies on GetSeries[0] have the proper result
			seriesList.OrderBy(a => a.AirDate ?? DateTime.MinValue); //FIX this might be null
			if (DefaultAnimeSeriesID.HasValue)
			{
				AnimeSeries series = repSeries.GetByID(DefaultAnimeSeriesID.Value);
				if (series != null)
				{
					seriesList.Remove(series);
					seriesList.Insert(0, series);
				}
			}
			return seriesList;
		}

		/*[XmlIgnore]
		public List<AnimeSeries> AllSeries
		{
			get
			{
				List<AnimeSeries> seriesList = new List<AnimeSeries>();
				AnimeGroup.GetAnimeSeriesRecursive(this.AnimeGroupID, ref seriesList);

				return seriesList;
			}
		}*/

		public List<AnimeSeries> GetAllSeries()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAllSeries(session);
			}
		}

		public List<AnimeSeries> GetAllSeries(ISession session)
		{
			List<AnimeSeries> seriesList = new List<AnimeSeries>();
			AnimeGroup.GetAnimeSeriesRecursive(session, this.AnimeGroupID, ref seriesList);

			return seriesList;
		}

		public string TagsString
		{
			get
			{
				string temp = "";
                foreach (AniDB_Tag tag in Tags)
                    temp += tag.TagName + "|";
				if (temp.Length > 2)
					temp = temp.Substring(0, temp.Length - 2);

				return temp;
			}
		}

		public List<AniDB_Tag> Tags
		{
			get
			{
                List<AniDB_Tag> tags = new List<AniDB_Tag>();
				List<int> animeTagIDs = new List<int>();
                List<AniDB_Anime_Tag> animeTags = new List<AniDB_Anime_Tag>();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // get a list of all the unique tags for this all the series in this group
                    foreach (AnimeSeries ser in GetAllSeries())
                    {
                        foreach (AniDB_Anime_Tag aac in ser.GetAnime().GetAnimeTags(session))
                        {
                            if (!animeTagIDs.Contains(aac.AniDB_Anime_TagID))
                            {
                                animeTagIDs.Add(aac.AniDB_Anime_TagID);
                                animeTags.Add(aac);
                            }
                        }
                    }

                    // now sort it by the weighting
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("Weight", true, SortType.eInteger));
                    animeTags = Sorting.MultiSort<AniDB_Anime_Tag>(animeTags, sortCriteria);

                    AniDB_TagRepository repTag = new AniDB_TagRepository();
                    foreach (AniDB_Anime_Tag animeTag in animeTags)
                    {
                        AniDB_Tag tag = repTag.GetByTagID(animeTag.TagID, session);
                        if (tag != null) tags.Add(tag);
                    }
                }
				
				return tags;
			}
		}

        public string CustomTagsString
        {
            get
            {
                string temp = "";
                foreach (CustomTag tag in CustomTags)
                {
                    if (!string.IsNullOrEmpty(temp))
                        temp += "|"; 
                    temp += tag.TagName; 
                }
                    
                return temp;
            }
        }

        public List<CustomTag> CustomTags
        {
            get
            {
                List<CustomTag> tags = new List<CustomTag>();
                List<int> tagIDs = new List<int>();

                CustomTagRepository repTags = new CustomTagRepository();

                // get a list of all the unique custom tags for all the series in this group
                foreach (AnimeSeries ser in GetAllSeries())
                {
                    foreach (CustomTag tag in repTags.GetByAnimeID(ser.AniDB_ID))
                    {
                        if (!tagIDs.Contains(tag.CustomTagID))
                        {
                            tagIDs.Add(tag.CustomTagID);
                            tags.Add(tag);
                        }
                    }
                }

                // now sort it by the tag name
                List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("TagName", false, SortType.eString));
                tags = Sorting.MultiSort<CustomTag>(tags, sortCriteria);

                return tags;
            }
        }

		public List<AniDB_Anime_Title> Titles
		{
			get
			{
				List<int> animeTitleIDs = new List<int>();
				List<AniDB_Anime_Title> animeTitles = new List<AniDB_Anime_Title>();


				// get a list of all the unique titles for this all the series in this group
				foreach (AnimeSeries ser in GetAllSeries())
				{
					foreach (AniDB_Anime_Title aat in ser.GetAnime().GetTitles())
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
				foreach (AnimeSeries ser in GetAllSeries())
				{
					AniDB_Vote vote = ser.GetAnime().UserVote;
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
				foreach (AnimeSeries ser in GetAllSeries())
				{
					AniDB_Vote vote = ser.GetAnime().UserVote;
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
				foreach (AnimeSeries ser in GetAllSeries())
				{
					AniDB_Vote vote = ser.GetAnime().UserVote;
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
				foreach (AnimeSeries ser in GetAllSeries())
				{
                    ser.UpdateStats(watchedStats, missingEpsStats, false);
				}
			}

			// now recursively update stats for all the child groups
			// and update the stats for the groups
			foreach (AnimeGroup grp in GetAllChildGroups())
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
			List<AnimeSeries> seriesList = GetAllSeries();

			JMMUserRepository repUsers = new JMMUserRepository();
			List<JMMUser> allUsers = repUsers.GetAll();

            if (missingEpsStats)
            {
                this.MissingEpisodeCount = 0;
                this.MissingEpisodeCountGroups = 0;

                foreach (AnimeSeries ser in seriesList)
                {
                    this.MissingEpisodeCount += ser.MissingEpisodeCount;
                    this.MissingEpisodeCountGroups += ser.MissingEpisodeCountGroups;
                    if (ser.LatestEpisodeAirDate.HasValue)
                    {
                        if ((LatestEpisodeAirDate.HasValue && ser.LatestEpisodeAirDate.Value>LatestEpisodeAirDate.Value) || (!LatestEpisodeAirDate.HasValue))
                            LatestEpisodeAirDate = ser.LatestEpisodeAirDate;
                    }
                }

                AnimeGroupRepository repGrp = new AnimeGroupRepository();
                repGrp.Save(this, true, false);
            }

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
		}

        public void UpdateGroupFilters(JMMUser usr)
        {
            AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();
            JMMUserRepository repUser = new JMMUserRepository();
            GroupFilterRepository repGrpFilter = new GroupFilterRepository();

            if (AnimeGroupParentID.HasValue)
                return;
            List<JMMUser> users = new List<JMMUser>();
            if (usr != null)
                users.Add(usr);
            else
                users = repUser.GetAll();
            foreach (JMMUser user in users)
            {
                AnimeGroup_User userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, AnimeGroupID);
                List<GroupFilter> gfs = repGrpFilter.GetAll();
                foreach (GroupFilter gf in gfs)
                {
                    bool change = false;
                    if (gf.EvaluateGroupFilter(this, user, userRec))
                    {
                        if (!gf.GroupsIds.ContainsKey(user.JMMUserID))
                        {
                            gf.GroupsIds[user.JMMUserID] = new HashSet<int>();
                        }
                        if (!gf.GroupsIds[user.JMMUserID].Contains(AnimeGroupID))
                        {
                            gf.GroupsIds[user.JMMUserID].Add(AnimeGroupID);
                            change = true;
                        }
                    }
                    else
                    {
                        if (gf.GroupsIds.ContainsKey(user.JMMUserID))
                        {
                            if (gf.GroupsIds[user.JMMUserID].Contains(AnimeGroupID))
                            {
                                gf.GroupsIds[user.JMMUserID].Remove(AnimeGroupID);
                                change = true;
                            }
                        }
                    }
                    if (change)
                        repGrpFilter.Save(gf, false, user);
                }
            }
        }




        public static void GetAnimeGroupsRecursive(ISession session, int animeGroupID, ref List<AnimeGroup> groupList)
		{
			AnimeGroupRepository rep = new AnimeGroupRepository();
			AnimeGroup grp = rep.GetByID(session, animeGroupID);
			if (grp == null) return;

			// get the child groups for this group
			groupList.AddRange(grp.GetChildGroups(session));

			foreach (AnimeGroup childGroup in grp.GetChildGroups(session))
			{
				GetAnimeGroupsRecursive(session, childGroup.AnimeGroupID, ref groupList);
			}
		}

		public static void GetAnimeSeriesRecursive(ISession session, int animeGroupID, ref List<AnimeSeries> seriesList)
		{
			AnimeGroupRepository rep = new AnimeGroupRepository();
			AnimeGroup grp = rep.GetByID(session, animeGroupID);
			if (grp == null) return;

			// get the series for this group
			List<AnimeSeries> thisSeries = grp.GetSeries(session);
			seriesList.AddRange(thisSeries);

			foreach (AnimeGroup childGroup in grp.GetChildGroups(session))
			{
				GetAnimeSeriesRecursive(session, childGroup.AnimeGroupID, ref seriesList);
			}
		}

		public AnimeGroup TopLevelAnimeGroup
		{
			get
			{
				if (!AnimeGroupParentID.HasValue) return this;
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup parentGroup = repGroups.GetByID(this.AnimeGroupParentID.Value);
				while (parentGroup!=null && parentGroup.AnimeGroupParentID.HasValue)
				{
					parentGroup = repGroups.GetByID(parentGroup.AnimeGroupParentID.Value);
				}
				return parentGroup;
			}
		}

	}
}
