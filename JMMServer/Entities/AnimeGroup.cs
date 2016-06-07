using System;
using System.Collections.Generic;
using System.IO;
using BinaryNorthwest;
using JMMContracts;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer.Entities
{
    public class AnimeGroup
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

        public List<string> AnimeTypesList
        {
            get
            {
                var atypeList = new List<string>();
                foreach (var series in GetAllSeries())
                {
                    var atype = series.GetAnime().AnimeTypeDescription;
                    if (!atypeList.Contains(atype)) atypeList.Add(atype);
                }
                return atypeList;
            }
        }

        public List<AniDB_Anime> Anime
        {
            get
            {
                var relAnime = new List<AniDB_Anime>();
                foreach (var serie in GetSeries())
                {
                    var anime = serie.GetAnime();
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
                    var totalVotes = 0;

                    foreach (var anime in Anime)
                    {
                        totalRating += anime.AniDBTotalRating;
                        totalVotes += anime.AniDBTotalVotes;
                    }

                    if (totalVotes == 0)
                        return 0;
                    return totalRating / totalVotes;
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  AniDBRating: {0}", ex.ToString());
                    return 0;
                }
            }
        }

        public string TagsString
        {
            get
            {
                var temp = "";
                foreach (var tag in Tags)
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
                var tags = new List<AniDB_Tag>();
                var animeTagIDs = new List<int>();
                var animeTags = new List<AniDB_Anime_Tag>();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // get a list of all the unique tags for this all the series in this group
                    foreach (var ser in GetAllSeries())
                    {
                        foreach (var aac in ser.GetAnime().GetAnimeTags(session))
                        {
                            if (!animeTagIDs.Contains(aac.AniDB_Anime_TagID))
                            {
                                animeTagIDs.Add(aac.AniDB_Anime_TagID);
                                animeTags.Add(aac);
                            }
                        }
                    }

                    // now sort it by the weighting
                    var sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("Weight", true, SortType.eInteger));
                    animeTags = Sorting.MultiSort(animeTags, sortCriteria);

                    var repTag = new AniDB_TagRepository();
                    foreach (var animeTag in animeTags)
                    {
                        var tag = repTag.GetByTagID(animeTag.TagID, session);
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
                var temp = "";
                foreach (var tag in CustomTags)
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
                var tags = new List<CustomTag>();
                var tagIDs = new List<int>();

                var repTags = new CustomTagRepository();

                // get a list of all the unique custom tags for all the series in this group
                foreach (var ser in GetAllSeries())
                {
                    foreach (var tag in repTags.GetByAnimeID(ser.AniDB_ID))
                    {
                        if (!tagIDs.Contains(tag.CustomTagID))
                        {
                            tagIDs.Add(tag.CustomTagID);
                            tags.Add(tag);
                        }
                    }
                }

                // now sort it by the tag name
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("TagName", false, SortType.eString));
                tags = Sorting.MultiSort(tags, sortCriteria);

                return tags;
            }
        }

        public List<AniDB_Anime_Title> Titles
        {
            get
            {
                var animeTitleIDs = new List<int>();
                var animeTitles = new List<AniDB_Anime_Title>();


                // get a list of all the unique titles for this all the series in this group
                foreach (var ser in GetAllSeries())
                {
                    foreach (var aat in ser.GetAnime().GetTitles())
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
                var temp = "";
                foreach (var title in Titles)
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
                var rep = new AdhocRepository();
                return rep.GetAllVideoQualityForGroup(AnimeGroupID);
            }
        }

        public decimal? UserVote
        {
            get
            {
                decimal totalVotes = 0;
                var countVotes = 0;
                foreach (var ser in GetAllSeries())
                {
                    var vote = ser.GetAnime().UserVote;
                    if (vote != null)
                    {
                        countVotes++;
                        totalVotes += vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                return totalVotes / countVotes / 100;
            }
        }

        public decimal? UserVotePermanent
        {
            get
            {
                decimal totalVotes = 0;
                var countVotes = 0;
                foreach (var ser in GetAllSeries())
                {
                    var vote = ser.GetAnime().UserVote;
                    if (vote != null && vote.VoteType == (int)AniDBVoteType.Anime)
                    {
                        countVotes++;
                        totalVotes += vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                return totalVotes / countVotes / 100;
            }
        }

        public decimal? UserVoteTemporary
        {
            get
            {
                decimal totalVotes = 0;
                var countVotes = 0;
                foreach (var ser in GetAllSeries())
                {
                    var vote = ser.GetAnime().UserVote;
                    if (vote != null && vote.VoteType == (int)AniDBVoteType.AnimeTemp)
                    {
                        countVotes++;
                        totalVotes += vote.VoteValue;
                    }
                }

                if (countVotes == 0)
                    return null;
                return totalVotes / countVotes / 100;
            }
        }

        public AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                if (!AnimeGroupParentID.HasValue) return this;
                var repGroups = new AnimeGroupRepository();
                var parentGroup = repGroups.GetByID(AnimeGroupParentID.Value);

                while (parentGroup.AnimeGroupParentID.HasValue)
                {
                    parentGroup = repGroups.GetByID(parentGroup.AnimeGroupParentID.Value);
                }
                return parentGroup;
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
            var allPosters = GetPosterFilenames(session);
            var posterName = "";
            if (allPosters.Count > 0)
                //posterName = allPosters[fanartRandom.Next(0, allPosters.Count)];
                posterName = allPosters[0];

            if (!string.IsNullOrEmpty(posterName))
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
            var allPosters = new List<string>();

            // check if user has specied a fanart to always be used
            if (DefaultAnimeSeriesID.HasValue)
            {
                var repSeries = new AnimeSeriesRepository();
                var defaultSeries = repSeries.GetByID(session, DefaultAnimeSeriesID.Value);
                if (defaultSeries != null)
                {
                    var anime = defaultSeries.GetAnime(session);
                    var defPosterPathNoBlanks = anime.GetDefaultPosterPathNoBlanks(session);

                    if (!string.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
                    {
                        allPosters.Add(defPosterPathNoBlanks);
                        return allPosters;
                    }
                }
            }

            foreach (var ser in GetAllSeries(session))
            {
                var anime = ser.GetAnime(session);
                var defPosterPathNoBlanks = anime.GetDefaultPosterPathNoBlanks(session);

                if (!string.IsNullOrEmpty(defPosterPathNoBlanks) && File.Exists(defPosterPathNoBlanks))
                    allPosters.Add(defPosterPathNoBlanks);
            }

            return allPosters;
        }

        public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(int animeid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetRelatedGroupsFromAnimeID(session, animeid);
            }
        }

        public static List<AnimeGroup> GetRelatedGroupsFromAnimeID(ISession session, int animeid)
        {
            var repAniAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            var repGroups = new AnimeGroupRepository();

            var grps = new List<AnimeGroup>();

            var anime = repAniAnime.GetByAnimeID(session, animeid);
            if (anime == null) return grps;

            // first check for groups which are directly related
            var relations = anime.GetRelatedAnime(session);
            foreach (var rel in relations)
            {
                var relationtype = rel.RelationType.ToLower();
                if ((relationtype == "same setting") || (relationtype == "alternative setting") ||
                    (relationtype == "character") || (relationtype == "other"))
                {
                    //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                    continue;
                }

                // we actually need to get the series, because it might have been added to another group already
                var ser = repSeries.GetByAnimeID(session, rel.RelatedAnimeID);
                if (ser != null)
                {
                    var grp = repGroups.GetByID(session, ser.AnimeGroupID);
                    if (grp != null) grps.Add(grp);
                }
            }
            if (grps.Count > 0) return grps;

            // if nothing found check by all related anime
            var relatedAnime = anime.GetAllRelatedAnime(session);
            foreach (var rel in relatedAnime)
            {
                // we actually need to get the series, because it might have been added to another group already
                var ser = repSeries.GetByAnimeID(session, rel.AnimeID);
                if (ser != null)
                {
                    var grp = repGroups.GetByID(session, ser.AnimeGroupID);
                    if (grp != null) grps.Add(grp);
                }
            }

            return grps;
        }

        public AnimeGroup_User GetUserRecord(int userID)
        {
            var repUser = new AnimeGroup_UserRepository();
            return repUser.GetByUserAndGroupID(userID, AnimeGroupID);
        }

        public AnimeGroup_User GetUserRecord(ISession session, int userID)
        {
            var repUser = new AnimeGroup_UserRepository();
            return repUser.GetByUserAndGroupID(session, userID, AnimeGroupID);
        }

        public void Populate(AnimeSeries series)
        {
            Description = series.GetAnime().Description;
            GroupName = series.GetAnime().PreferredTitle;
            SortName = series.GetAnime().PreferredTitle;
            DateTimeUpdated = DateTime.Now;
            DateTimeCreated = DateTime.Now;
        }

        /// <summary>
        ///     Renames all Anime groups based on the user's language preferences
        /// </summary>
        public static void RenameAllGroups()
        {
            var repGroups = new AnimeGroupRepository();
            var groupsToSave = new List<AnimeGroup>();
            foreach (var grp in repGroups.GetAll())
            {
                // only rename the group if it has one direct child Anime Series
                if (grp.GetSeries().Count == 1)
                {
                    var newTitle = grp.GetSeries()[0].GetAnime().PreferredTitle;
                    grp.GroupName = newTitle;
                    grp.SortName = newTitle;
                    groupsToSave.Add(grp);
                    repGroups.Save(grp);
                }
            }

            foreach (var grp in groupsToSave)
                repGroups.Save(grp);
        }

        public Contract_AnimeGroup ToContract(AnimeGroup_User userRecord)
        {
            var contract = new Contract_AnimeGroup();
            contract.AnimeGroupID = AnimeGroupID;
            contract.AnimeGroupParentID = AnimeGroupParentID;
            contract.DefaultAnimeSeriesID = DefaultAnimeSeriesID;
            contract.GroupName = GroupName;
            contract.Description = Description;
            contract.SortName = SortName;
            contract.EpisodeAddedDate = EpisodeAddedDate;
            contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
            contract.OverrideDescription = OverrideDescription;
            contract.DateTimeUpdated = DateTimeUpdated;

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

            contract.MissingEpisodeCount = MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;

            if (StatsCache.Instance.StatGroupAudioLanguages.ContainsKey(AnimeGroupID))
                contract.Stat_AudioLanguages = StatsCache.Instance.StatGroupAudioLanguages[AnimeGroupID];
            else contract.Stat_AudioLanguages = "";

            if (StatsCache.Instance.StatGroupSubtitleLanguages.ContainsKey(AnimeGroupID))
                contract.Stat_SubtitleLanguages = StatsCache.Instance.StatGroupSubtitleLanguages[AnimeGroupID];
            else contract.Stat_SubtitleLanguages = "";

            if (StatsCache.Instance.StatGroupVideoQuality.ContainsKey(AnimeGroupID))
                contract.Stat_AllVideoQuality = StatsCache.Instance.StatGroupVideoQuality[AnimeGroupID];
            else contract.Stat_AllVideoQuality = "";

            if (StatsCache.Instance.StatGroupVideoQualityEpisodes.ContainsKey(AnimeGroupID))
                contract.Stat_AllVideoQuality_Episodes = StatsCache.Instance.StatGroupVideoQualityEpisodes[AnimeGroupID];
            else contract.Stat_AllVideoQuality_Episodes = "";

            if (StatsCache.Instance.StatGroupIsComplete.ContainsKey(AnimeGroupID))
                contract.Stat_IsComplete = StatsCache.Instance.StatGroupIsComplete[AnimeGroupID];
            else contract.Stat_IsComplete = false;

            if (StatsCache.Instance.StatGroupHasTvDB.ContainsKey(AnimeGroupID))
                contract.Stat_HasTvDBLink = StatsCache.Instance.StatGroupHasTvDB[AnimeGroupID];
            else contract.Stat_HasTvDBLink = false;

            if (StatsCache.Instance.StatGroupHasMAL.ContainsKey(AnimeGroupID))
                contract.Stat_HasMALLink = StatsCache.Instance.StatGroupHasMAL[AnimeGroupID];
            else contract.Stat_HasMALLink = false;

            if (StatsCache.Instance.StatGroupHasMovieDB.ContainsKey(AnimeGroupID))
                contract.Stat_HasMovieDBLink = StatsCache.Instance.StatGroupHasMovieDB[AnimeGroupID];
            else contract.Stat_HasMovieDBLink = false;

            if (StatsCache.Instance.StatGroupHasMovieDBOrTvDB.ContainsKey(AnimeGroupID))
                contract.Stat_HasMovieDBOrTvDBLink = StatsCache.Instance.StatGroupHasMovieDBOrTvDB[AnimeGroupID];
            else contract.Stat_HasMovieDBOrTvDBLink = false;

            if (StatsCache.Instance.StatGroupIsFinishedAiring.ContainsKey(AnimeGroupID))
                contract.Stat_HasFinishedAiring = StatsCache.Instance.StatGroupIsFinishedAiring[AnimeGroupID];
            else contract.Stat_HasFinishedAiring = false;

            if (StatsCache.Instance.StatGroupIsCurrentlyAiring.ContainsKey(AnimeGroupID))
                contract.Stat_IsCurrentlyAiring = StatsCache.Instance.StatGroupIsCurrentlyAiring[AnimeGroupID];
            else contract.Stat_IsCurrentlyAiring = false;

            if (StatsCache.Instance.StatGroupAirDate_Max.ContainsKey(AnimeGroupID))
                contract.Stat_AirDate_Max = StatsCache.Instance.StatGroupAirDate_Max[AnimeGroupID];
            else contract.Stat_AirDate_Max = null;

            if (StatsCache.Instance.StatGroupAirDate_Min.ContainsKey(AnimeGroupID))
                contract.Stat_AirDate_Min = StatsCache.Instance.StatGroupAirDate_Min[AnimeGroupID];
            else contract.Stat_AirDate_Min = null;

            if (StatsCache.Instance.StatGroupTags.ContainsKey(AnimeGroupID))
                contract.Stat_AllTags = StatsCache.Instance.StatGroupTags[AnimeGroupID];
            else contract.Stat_AllTags = "";

            if (StatsCache.Instance.StatGroupCustomTags.ContainsKey(AnimeGroupID))
                contract.Stat_AllCustomTags = StatsCache.Instance.StatGroupCustomTags[AnimeGroupID];
            else contract.Stat_AllCustomTags = "";

            if (StatsCache.Instance.StatGroupEndDate.ContainsKey(AnimeGroupID))
                contract.Stat_EndDate = StatsCache.Instance.StatGroupEndDate[AnimeGroupID];
            else contract.Stat_EndDate = null;

            if (StatsCache.Instance.StatGroupSeriesCreatedDate.ContainsKey(AnimeGroupID))
                contract.Stat_SeriesCreatedDate = StatsCache.Instance.StatGroupSeriesCreatedDate[AnimeGroupID];
            else contract.Stat_SeriesCreatedDate = null;

            if (StatsCache.Instance.StatGroupTitles.ContainsKey(AnimeGroupID))
                contract.Stat_AllTitles = StatsCache.Instance.StatGroupTitles[AnimeGroupID];
            else contract.Stat_AllTitles = "";

            if (StatsCache.Instance.StatGroupUserVoteOverall.ContainsKey(AnimeGroupID))
                contract.Stat_UserVoteOverall = StatsCache.Instance.StatGroupUserVoteOverall[AnimeGroupID];
            else contract.Stat_UserVoteOverall = null;

            if (StatsCache.Instance.StatGroupUserVotePermanent.ContainsKey(AnimeGroupID))
                contract.Stat_UserVotePermanent = StatsCache.Instance.StatGroupUserVotePermanent[AnimeGroupID];
            else contract.Stat_UserVotePermanent = null;

            if (StatsCache.Instance.StatGroupUserVoteTemporary.ContainsKey(AnimeGroupID))
                contract.Stat_UserVoteTemporary = StatsCache.Instance.StatGroupUserVoteTemporary[AnimeGroupID];
            else contract.Stat_UserVoteTemporary = null;

            if (StatsCache.Instance.StatGroupSeriesCount.ContainsKey(AnimeGroupID))
                contract.Stat_SeriesCount = StatsCache.Instance.StatGroupSeriesCount[AnimeGroupID];
            else contract.Stat_SeriesCount = 0;

            if (StatsCache.Instance.StatGroupEpisodeCount.ContainsKey(AnimeGroupID))
                contract.Stat_EpisodeCount = StatsCache.Instance.StatGroupEpisodeCount[AnimeGroupID];
            else contract.Stat_EpisodeCount = 0;

            if (StatsCache.Instance.StatGroupAniDBRating.ContainsKey(AnimeGroupID))
                contract.Stat_AniDBRating = StatsCache.Instance.StatGroupAniDBRating[AnimeGroupID];
            else contract.Stat_AniDBRating = 0;

            //contract.AniDB_AirDate = this.AirDate;
            //contract.AniDB_Year = animeRec.Year;

            return contract;
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
            var repGroups = new AnimeGroupRepository();
            return repGroups.GetByParentID(session, AnimeGroupID);
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
            var grpList = new List<AnimeGroup>();
            GetAnimeGroupsRecursive(session, AnimeGroupID, ref grpList);
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
            var repSeries = new AnimeSeriesRepository();
            var seriesList = repSeries.GetByGroupID(AnimeGroupID);

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
            var seriesList = new List<AnimeSeries>();
            GetAnimeSeriesRecursive(session, AnimeGroupID, ref seriesList);

            return seriesList;
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
        ///     Update stats for all child groups and series
        ///     This should only be called from the very top level group.
        /// </summary>
        public void UpdateStatsFromTopLevel(bool updateGroupStatsOnly, bool watchedStats, bool missingEpsStats)
        {
            if (AnimeGroupParentID.HasValue) return;

            // update the stats for all the sries first
            if (!updateGroupStatsOnly)
            {
                foreach (var ser in GetAllSeries())
                {
                    ser.UpdateStats(watchedStats, missingEpsStats, false);
                }
            }

            // now recursively update stats for all the child groups
            // and update the stats for the groups
            foreach (var grp in GetAllChildGroups())
            {
                grp.UpdateStats(watchedStats, missingEpsStats);
            }

            UpdateStats(watchedStats, missingEpsStats);
        }

        /// <summary>
        ///     Update the stats for this group based on the child series
        ///     Assumes that all the AnimeSeries have had their stats updated already
        /// </summary>
        public void UpdateStats(bool watchedStats, bool missingEpsStats)
        {
            var seriesList = GetAllSeries();

            var repUsers = new JMMUserRepository();
            var allUsers = repUsers.GetAll();

            if (watchedStats)
            {
                foreach (var juser in allUsers)
                {
                    var userRecord = GetUserRecord(juser.JMMUserID);
                    if (userRecord == null) userRecord = new AnimeGroup_User(juser.JMMUserID, AnimeGroupID);

                    // reset stats
                    userRecord.WatchedCount = 0;
                    userRecord.UnwatchedEpisodeCount = 0;
                    userRecord.PlayedCount = 0;
                    userRecord.StoppedCount = 0;
                    userRecord.WatchedEpisodeCount = 0;
                    userRecord.WatchedDate = null;

                    foreach (var ser in seriesList)
                    {
                        var serUserRecord = ser.GetUserRecord(juser.JMMUserID);
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
                    logger.Trace("Updating stats for {0}", ToString());
                    var rep = new AnimeGroup_UserRepository();
                    rep.Save(userRecord);
                }
            }

            if (missingEpsStats)
            {
                MissingEpisodeCount = 0;
                MissingEpisodeCountGroups = 0;

                foreach (var ser in seriesList)
                {
                    MissingEpisodeCount += ser.MissingEpisodeCount;
                    MissingEpisodeCountGroups += ser.MissingEpisodeCountGroups;
                }

                var repGrp = new AnimeGroupRepository();
                repGrp.Save(this);
            }
        }

        public static void GetAnimeGroupsRecursive(ISession session, int animeGroupID, ref List<AnimeGroup> groupList)
        {
            var rep = new AnimeGroupRepository();
            var grp = rep.GetByID(session, animeGroupID);
            if (grp == null) return;

            // get the child groups for this group
            groupList.AddRange(grp.GetChildGroups(session));

            foreach (var childGroup in grp.GetChildGroups(session))
            {
                GetAnimeGroupsRecursive(session, childGroup.AnimeGroupID, ref groupList);
            }
        }

        public static void GetAnimeSeriesRecursive(ISession session, int animeGroupID, ref List<AnimeSeries> seriesList)
        {
            var rep = new AnimeGroupRepository();
            var grp = rep.GetByID(session, animeGroupID);
            if (grp == null) return;

            // get the series for this group
            var thisSeries = grp.GetSeries(session);
            seriesList.AddRange(thisSeries);

            foreach (var childGroup in grp.GetChildGroups(session))
            {
                GetAnimeSeriesRecursive(session, childGroup.AnimeGroupID, ref seriesList);
            }
        }

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

        #endregion
    }
}