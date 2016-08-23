using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniDBAPI;

using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.LZ4;
using JMMServer.Repositories;
using NHibernate;
using NLog;

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
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }

        #endregion

        public const int CONTRACT_VERSION = 4;


        private static Logger logger = LogManager.GetCurrentClassLogger();


        internal Contract_AnimeGroup _contract = null;

        public virtual Contract_AnimeGroup Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<Contract_AnimeGroup>(ContractBlob, ContractSize);
                return _contract;
            }
            set
            {
                _contract = value;
                int outsize;
                ContractBlob = CompressionHelper.SerializeObject(value, out outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _contract = null;
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

                    if (!String.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
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

                if (!String.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
                    allPosters.Add(defPosterPathNoBlanks);
            }

            return allPosters;
        }

        public Contract_AnimeGroup GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null)
        {
            if (Contract == null)
                return new Contract_AnimeGroup();
            Contract_AnimeGroup contract = (Contract_AnimeGroup) Contract.DeepCopy();
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
            else if (types != null)
            {
                if (!types.Contains(GroupFilterConditionType.HasUnwatchedEpisodes))
                    types.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
                if (!types.Contains(GroupFilterConditionType.Favourite))
                    types.Add(GroupFilterConditionType.Favourite);
                if (!types.Contains(GroupFilterConditionType.EpisodeWatchedDate))
                    types.Add(GroupFilterConditionType.EpisodeWatchedDate);
                if (!types.Contains(GroupFilterConditionType.HasWatchedEpisodes))
                    types.Add(GroupFilterConditionType.HasWatchedEpisodes);
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
            rr = new AnimeGroup_User(userid, this.AnimeGroupID);
            rr.WatchedCount = 0;
            rr.UnwatchedEpisodeCount = 0;
            rr.PlayedCount = 0;
            rr.StoppedCount = 0;
            rr.WatchedEpisodeCount = 0;
            rr.WatchedDate = null;
            AnimeGroup_UserRepository repo = new AnimeGroup_UserRepository();
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

        public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(ISession session, int animeid,
            bool forceRecursive = false)
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
            get { return MissingEpisodeCount > 0 || MissingEpisodeCountGroups > 0; }
        }

        public bool HasMissingEpisodesGroups
        {
            get { return MissingEpisodeCountGroups > 0; }
        }

        public bool HasMissingEpisodes
        {
            get { return MissingEpisodeCountGroups > 0; }
        }

        /*
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
        */

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
					new AnimeSeriesRepository().Save(list[0], false);
					list[0].UpdateStats(true, true, false);
					string newTitle = list[0].GetSeriesName();
                    grp.GroupName = newTitle;
                    grp.SortName = newTitle;
                    repGroups.Save(grp, true, true);
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
								#region tvdb names
								List<TvDB_Series> tvdbs = ser.GetTvDBSeries();
								if(tvdbs != null && tvdbs.Count != 0)
								{
									foreach (TvDB_Series tvdbser in tvdbs)
									{
										if (tvdbser.SeriesName.Equals(grp.GroupName))
										{
											hasCustomName = false;
											break;
										}
									}
								}
								#endregion
								new AnimeSeriesRepository().Save(ser, false);
								series.UpdateStats(true, true, false);
								if (series == null)
								{
									series = ser;
									continue;
								}
								if (ser.AirDate < series.AirDate) series = ser;
							}
                        }
                    }
                    if (series != null)
                    {
                        string newTitle = series.GetSeriesName();
						if (grp.DefaultAnimeSeriesID.HasValue &&
							grp.DefaultAnimeSeriesID.Value != series.AnimeSeriesID)
							newTitle = new AnimeSeriesRepository().GetByID(grp.DefaultAnimeSeriesID.Value).GetSeriesName();
                        if (hasCustomName) newTitle = grp.GroupName;
                        // reset tags, description, etc to new series
                        grp.Populate(series);
                        grp.GroupName = newTitle;
                        grp.SortName = newTitle;
                        repGroups.Save(grp, true, true);
						grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
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
                        totalRating += (decimal) anime.AniDBTotalRating;
                        totalVotes += anime.AniDBTotalVotes;
                    }

                    if (totalVotes == 0)
                        return 0;
                    else
                        return totalRating/(decimal) totalVotes;
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

        /*
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
		*/

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

                    AniDB_TagRepository repTag = new AniDB_TagRepository();
                    foreach (AniDB_Anime_Tag animeTag in animeTags.OrderByDescending(a=>a.Weight))
                    {
                        AniDB_Tag tag = repTag.GetByTagID(animeTag.TagID, session);
                        if (tag != null) tags.Add(tag);
                    }
                }

                return tags;
            }
        }

        /*
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
		*/

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

                return tags.OrderBy(a=>a.TagName).ToList();
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

        /*
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
		*/

        public HashSet<string> VideoQualities
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
                        totalVotes += (decimal) vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                else
                    return totalVotes/(decimal) countVotes/(decimal) 100;
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
                    if (vote != null && vote.VoteType == (int) AniDBVoteType.Anime)
                    {
                        countVotes++;
                        totalVotes += (decimal) vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                else
                    return totalVotes/(decimal) countVotes/(decimal) 100;
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
                    if (vote != null && vote.VoteType == (int) AniDBVoteType.AnimeTemp)
                    {
                        countVotes++;
                        totalVotes += (decimal) vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                else
                    return totalVotes/(decimal) countVotes/(decimal) 100;
            }
        }

        public override string ToString()
        {
            return String.Format("Group: {0} ({1})", GroupName, AnimeGroupID);
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
	                // Now ser.LatestEpisodeAirDate should never be greater than today
                    if (ser.LatestEpisodeAirDate.HasValue)
                    {
                        if ((LatestEpisodeAirDate.HasValue &&
                             ser.LatestEpisodeAirDate.Value > LatestEpisodeAirDate.Value) ||
                            !LatestEpisodeAirDate.HasValue)
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


        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(Contract_AnimeGroup oldcontract,
            Contract_AnimeGroup newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();
            if (oldcontract == null || oldcontract.Stat_IsComplete != newcontract.Stat_IsComplete)
                h.Add(GroupFilterConditionType.CompletedSeries);
            if (oldcontract == null ||
                (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) !=
                (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
                h.Add(GroupFilterConditionType.MissingEpisodes);
            if (oldcontract == null || !oldcontract.Stat_AllTags.SetEquals(newcontract.Stat_AllTags))
                h.Add(GroupFilterConditionType.Tag);
            if (oldcontract == null || oldcontract.Stat_AirDate_Min != newcontract.Stat_AirDate_Min ||
                oldcontract.Stat_AirDate_Max != newcontract.Stat_AirDate_Max)
                h.Add(GroupFilterConditionType.AirDate);
            if (oldcontract == null || oldcontract.Stat_HasTvDBLink != newcontract.Stat_HasTvDBLink)
                h.Add(GroupFilterConditionType.AssignedTvDBInfo);
            if (oldcontract == null || oldcontract.Stat_HasMALLink != newcontract.Stat_HasMALLink)
                h.Add(GroupFilterConditionType.AssignedMALInfo);
            if (oldcontract == null || oldcontract.Stat_HasMovieDBLink != newcontract.Stat_HasMovieDBLink)
                h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
            if (oldcontract == null || oldcontract.Stat_HasMovieDBOrTvDBLink != newcontract.Stat_HasMovieDBOrTvDBLink)
                h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
            if (oldcontract == null || !oldcontract.Stat_AnimeTypes.SetEquals(newcontract.Stat_AnimeTypes))
                h.Add(GroupFilterConditionType.AnimeType);
            if (oldcontract == null || !oldcontract.Stat_AllVideoQuality.SetEquals(newcontract.Stat_AllVideoQuality) ||
                !oldcontract.Stat_AllVideoQuality_Episodes.SetEquals(newcontract.Stat_AllVideoQuality_Episodes))
                h.Add(GroupFilterConditionType.VideoQuality);
            if (oldcontract == null || oldcontract.AnimeGroupID != newcontract.AnimeGroupID)
                h.Add(GroupFilterConditionType.AnimeGroup);
            if (oldcontract == null || oldcontract.Stat_AniDBRating != newcontract.Stat_AniDBRating)
                h.Add(GroupFilterConditionType.AniDBRating);
            if (oldcontract == null || oldcontract.Stat_SeriesCreatedDate != newcontract.Stat_SeriesCreatedDate)
                h.Add(GroupFilterConditionType.SeriesCreatedDate);
            if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
                h.Add(GroupFilterConditionType.EpisodeAddedDate);
            if (oldcontract == null || oldcontract.Stat_HasFinishedAiring != newcontract.Stat_HasFinishedAiring ||
                oldcontract.Stat_IsCurrentlyAiring != newcontract.Stat_IsCurrentlyAiring)
                h.Add(GroupFilterConditionType.FinishedAiring);
            if (oldcontract == null ||
                oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
                h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
            if (oldcontract == null || !oldcontract.Stat_AudioLanguages.SetEquals(newcontract.Stat_AudioLanguages))
                h.Add(GroupFilterConditionType.AudioLanguage);
            if (oldcontract == null || !oldcontract.Stat_SubtitleLanguages.SetEquals(newcontract.Stat_SubtitleLanguages))
                h.Add(GroupFilterConditionType.SubtitleLanguage);
            if (oldcontract == null || oldcontract.Stat_EpisodeCount != newcontract.Stat_EpisodeCount)
                h.Add(GroupFilterConditionType.EpisodeCount);
            if (oldcontract == null || !oldcontract.Stat_AllCustomTags.SetEquals(newcontract.Stat_AllCustomTags))
                h.Add(GroupFilterConditionType.CustomTags);
            if (oldcontract == null || oldcontract.LatestEpisodeAirDate != newcontract.LatestEpisodeAirDate)
                h.Add(GroupFilterConditionType.LatestEpisodeAirDate);
            int oldyear = -1;
            int newyear = -1;
            if (oldcontract?.Stat_AirDate_Min != null)
                oldyear = oldcontract.Stat_AirDate_Min.Value.Year;
            if (newcontract?.Stat_AirDate_Min != null)
                newyear = newcontract.Stat_AirDate_Min.Value.Year;
            if (oldyear != newyear)
                h.Add(GroupFilterConditionType.Year);

            //TODO This two should be moved to AnimeGroup_User in the future...
            if (oldcontract == null || oldcontract.Stat_UserVotePermanent != newcontract.Stat_UserVotePermanent)
                h.Add(GroupFilterConditionType.UserVoted);

            if (oldcontract == null || oldcontract.Stat_UserVoteOverall != newcontract.Stat_UserVoteOverall)
            {
                h.Add(GroupFilterConditionType.UserRating);
                h.Add(GroupFilterConditionType.UserVotedAny);
            }
            return h;
        }

        public HashSet<GroupFilterConditionType> UpdateContract(ISession session, bool updatestats)
        {
            Contract_AnimeGroup contract = (Contract_AnimeGroup) Contract?.DeepCopy();
            if (contract == null)
            {
                contract = new Contract_AnimeGroup();
                updatestats = true;
            }
            contract.AnimeGroupID = AnimeGroupID;
            contract.AnimeGroupParentID = AnimeGroupParentID;
            contract.DefaultAnimeSeriesID = DefaultAnimeSeriesID;
            contract.GroupName = GroupName;
            contract.Description = Description;
            contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
            contract.SortName = SortName;
            contract.EpisodeAddedDate = EpisodeAddedDate;
            contract.OverrideDescription = OverrideDescription;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.IsFave = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedEpisodeCount = 0;
            contract.WatchedDate = null;
            contract.PlayedCount = 0;
            contract.WatchedCount = 0;
            contract.StoppedCount = 0;
            contract.MissingEpisodeCount = MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;

            List<AnimeSeries> series = GetAllSeries(session);
            if (updatestats)
            {
                DateTime? airDate_Min = null;
                DateTime? airDate_Max = null;
                DateTime? endDate = new DateTime(1980, 1, 1);
                DateTime? seriesCreatedDate = null;
                bool isComplete = false;
                bool hasFinishedAiring = false;
                bool isCurrentlyAiring = false;
                HashSet<string> videoQualityEpisodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                bool hasTvDB = true;
                bool hasMAL = true;
                bool hasMovieDB = true;
                bool hasMovieDBOrTvDB = true;
                int seriesCount = 0;
                int epCount = 0;
                AdhocRepository repAdHoc = new AdhocRepository();
                VideoLocalRepository repVids = new VideoLocalRepository();
                CrossRef_File_EpisodeRepository repXrefs = new CrossRef_File_EpisodeRepository();
                foreach (AnimeSeries serie in series)
                {
                    seriesCount++;
                    List<VideoLocal> vidsTemp = repVids.GetByAniDBAnimeID(session, serie.AniDB_ID);
                    List<CrossRef_File_Episode> crossRefs = repXrefs.GetByAnimeID(session, serie.AniDB_ID);

                    Dictionary<int, List<CrossRef_File_Episode>> dictCrossRefs =
                        new Dictionary<int, List<CrossRef_File_Episode>>();
                    foreach (CrossRef_File_Episode xref in crossRefs)
                    {
                        if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                            dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                        dictCrossRefs[xref.EpisodeID].Add(xref);
                    }

                    Dictionary<string, VideoLocal> dictVids = new Dictionary<string, VideoLocal>();
                    foreach (VideoLocal vid in vidsTemp)
                    {
                        //Hashes may be repeated from multiple locations but we don't care
                        dictVids[vid.Hash] = vid;
                    }
                    // All Video Quality Episodes
                    // Try to determine if this anime has all the episodes available at a certain video quality
                    // e.g.  the series has all episodes in blu-ray
                    // Also look at languages
                    Dictionary<string, int> vidQualEpCounts = new Dictionary<string, int>();
                    // video quality, count of episodes
                    AniDB_Anime anime = serie.GetAnime(session);
                    bool shouldsaveanime = false;
                    foreach (AnimeEpisode ep in serie.GetAnimeEpisodes(session))
                    {
                        if (ep.EpisodeTypeEnum != enEpisodeType.Episode) continue;


                        List<VideoLocal> epVids = new List<VideoLocal>();
                        if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                        {
                            foreach (CrossRef_File_Episode xref in dictCrossRefs[ep.AniDB_EpisodeID])
                            {
                                if (xref.EpisodeID == ep.AniDB_EpisodeID)
                                {
                                    if (dictVids.ContainsKey(xref.Hash))
                                        epVids.Add(dictVids[xref.Hash]);
                                }
                            }
                        }

                        List<string> qualityAddedSoFar = new List<string>();
                        // handle mutliple files of the same quality for one episode
                        foreach (VideoLocal vid in epVids)
                        {
                            AniDB_File anifile = vid.GetAniDBFile(session);
                            if (anifile == null) continue;

                            if (!qualityAddedSoFar.Contains(anifile.File_Source))
                            {
                                if (!vidQualEpCounts.ContainsKey(anifile.File_Source))
                                    vidQualEpCounts[anifile.File_Source] = 1;
                                else
                                    vidQualEpCounts[anifile.File_Source]++;

                                qualityAddedSoFar.Add(anifile.File_Source);
                            }
                        }
                    }
                    epCount = epCount + anime.EpisodeCountNormal;

                    foreach (KeyValuePair<string, int> kvp in vidQualEpCounts)
                    {
                        if (!videoQualityEpisodes.Contains(kvp.Key))
                        {
                            if (anime.EpisodeCountNormal == kvp.Value)
                            {
                                videoQualityEpisodes.Add(kvp.Key);
                            }
                        }
                    }
                    // audio languages
                    Dictionary<int, LanguageStat> dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(session,
                        anime.AnimeID);
                    foreach (KeyValuePair<int, LanguageStat> kvp in dicAudio)
                    {
                        foreach (string lanName in kvp.Value.LanguageNames)
                        {
                            if (!audioLanguages.Contains(lanName))
                                audioLanguages.Add(lanName);
                        }
                    }
                    // subtitle languages
                    Dictionary<int, LanguageStat> dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(session,
                        anime.AnimeID);
                    foreach (KeyValuePair<int, LanguageStat> kvp in dicSubtitle)
                    {
                        foreach (string lanName in kvp.Value.LanguageNames)
                        {
                            if (!subtitleLanguages.Contains(lanName))
                                subtitleLanguages.Add(lanName);
                        }
                    }

                    // Calculate Air Date 
                    DateTime? thisDate = serie.AirDate;
                    if (thisDate.HasValue)
                    {
                        if (airDate_Min.HasValue)
                        {
                            if (thisDate.Value < airDate_Min.Value) airDate_Min = thisDate;
                        }
                        else
                            airDate_Min = thisDate;

                        if (airDate_Max.HasValue)
                        {
                            if (thisDate.Value > airDate_Max.Value) airDate_Max = thisDate;
                        }
                        else
                            airDate_Max = thisDate;
                    }

                    // calculate end date
                    // if the end date is NULL it actually means it is ongoing, so this is the max possible value
                    thisDate = serie.EndDate;
                    if (thisDate.HasValue && endDate.HasValue)
                    {
                        if (thisDate.Value > endDate.Value) endDate = thisDate;
                    }
                    else
                        endDate = null;

                    // Note - only one series has to be finished airing to qualify
                    if (serie.EndDate.HasValue && serie.EndDate.Value < DateTime.Now)
                        hasFinishedAiring = true;

                    // Note - only one series has to be finished airing to qualify
                    if (!serie.EndDate.HasValue || serie.EndDate.Value > DateTime.Now)
                        isCurrentlyAiring = true;

                    // We evaluate IsComplete as true if
                    // 1. series has finished airing
                    // 2. user has all episodes locally
                    // Note - only one series has to be complete for the group to be considered complete
                    if (serie.EndDate.HasValue)
                    {
                        if (serie.EndDate.Value < DateTime.Now && serie.MissingEpisodeCount == 0 &&
                            serie.MissingEpisodeCountGroups == 0)
                        {
                            isComplete = true;
                        }
                    }

                    // Calculate Series Created Date 
                    thisDate = serie.DateTimeCreated;
                    if (thisDate.HasValue)
                    {
                        if (seriesCreatedDate.HasValue)
                        {
                            if (thisDate.Value < seriesCreatedDate.Value) seriesCreatedDate = thisDate;
                        }
                        else
                            seriesCreatedDate = thisDate;
                    }
                    // for the group, if any of the series don't have a tvdb link
                    // we will consider the group as not having a tvdb link

                    List<CrossRef_AniDB_TvDBV2> tvXrefs = serie.GetCrossRefTvDBV2();

                    if (tvXrefs == null || tvXrefs.Count == 0) hasTvDB = false;
                    if (serie.CrossRefMovieDB == null) hasMovieDB = false;
                    if (serie.CrossRefMAL == null) hasMAL = false;

                    if ((tvXrefs == null || tvXrefs.Count == 0) && serie.CrossRefMovieDB == null)
                        hasMovieDBOrTvDB = false;
                }

                contract.Stat_AllTags = new HashSet<string>(Tags.Select(a => a.TagName).Distinct(StringComparer.InvariantCultureIgnoreCase),StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllCustomTags = new HashSet<string>(CustomTags.Select(a => a.TagName).Distinct(StringComparer.InvariantCultureIgnoreCase), StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllTitles = new HashSet<string>(Titles.Select(a => a.Title).Distinct(StringComparer.InvariantCultureIgnoreCase), StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AnimeTypes =
                    new HashSet<string>(
                        series.Select(a => a.Contract?.AniDBAnime?.AniDBAnime)
                            .Where(a => a != null)
                            .Select(a => a.AnimeType.ToString())
                            .Distinct(StringComparer.InvariantCultureIgnoreCase), StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllVideoQuality = VideoQualities;
                contract.Stat_IsComplete = isComplete;
                contract.Stat_HasFinishedAiring = hasFinishedAiring;
                contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
                contract.Stat_HasTvDBLink = hasTvDB;
                contract.Stat_HasMALLink = hasMAL;
                contract.Stat_HasMovieDBLink = hasMovieDB;
                contract.Stat_HasMovieDBOrTvDBLink = hasMovieDBOrTvDB;
                contract.Stat_SeriesCount = seriesCount;
                contract.Stat_EpisodeCount = epCount;
                contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
                contract.Stat_AirDate_Min = airDate_Min;
                contract.Stat_AirDate_Max = airDate_Max;
                contract.Stat_EndDate = endDate;
                contract.Stat_SeriesCreatedDate = seriesCreatedDate;
                contract.Stat_UserVoteOverall = UserVote;
                contract.Stat_UserVotePermanent = UserVotePermanent;
                contract.Stat_UserVoteTemporary = UserVoteTemporary;
                contract.Stat_AniDBRating = AniDBRating;
                contract.Stat_AudioLanguages = audioLanguages;
                contract.Stat_SubtitleLanguages = subtitleLanguages;
                contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
            }
            HashSet<GroupFilterConditionType> types = GetConditionTypesChanged(Contract, contract);
            Contract = contract;
            return types;
        }

        public void DeleteFromFilters()
        {
            GroupFilterRepository repo = new GroupFilterRepository();
            foreach (GroupFilter gf in repo.GetAll())
            {
                bool change = false;
                foreach (int k in gf.GroupsIds.Keys)
                {
                    if (gf.GroupsIds[k].Contains(AnimeGroupID))
                    {
                        gf.GroupsIds[k].Remove(AnimeGroupID);
                        change = true;
                    }
                }
                if (change)
                    repo.Save(gf);
            }
        }

        public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, JMMUser user = null)
        {
            GroupFilterRepository repos = new GroupFilterRepository();
            JMMUserRepository urepo = new JMMUserRepository();

            List<JMMUser> users = new List<JMMUser> {user};
            if (user == null)
                users = urepo.GetAll();
            List<GroupFilter> tosave = new List<GroupFilter>();

            foreach (JMMUser u in users)
            {
                HashSet<GroupFilterConditionType> n = new HashSet<GroupFilterConditionType>(types);
                Contract_AnimeGroup cgrp = GetUserContract(u.JMMUserID, n);
                foreach (GroupFilter gf in repos.GetWithConditionTypesAndAll(n))
                {
                    if (gf.CalculateGroupFilterGroups(cgrp, u.Contract, u.JMMUserID))
                    {
                        if (!tosave.Contains(gf))
                            tosave.Add(gf);
                    }
                }
            }
            foreach (GroupFilter gf in tosave)
            {
                repos.Save(gf);
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
                while (parentGroup != null && parentGroup.AnimeGroupParentID.HasValue)
                {
                    parentGroup = repGroups.GetByID(parentGroup.AnimeGroupParentID.Value);
                }
                return parentGroup;
            }
        }
    }
}