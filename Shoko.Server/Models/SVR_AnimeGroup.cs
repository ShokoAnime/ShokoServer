using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Tasks;

namespace Shoko.Server.Models
{
    public class SVR_AnimeGroup : AnimeGroup
    {
        #region DB Columns

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }


        public const int CONTRACT_VERSION = 6;


        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        internal CL_AnimeGroup_User _contract;

        [NotMapped]
        public virtual CL_AnimeGroup_User Contract
        {
            get
            {
                if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
                {
                    _contract=new CL_AnimeGroup_User(new SeasonComparator());
                    CompressionHelper.PopulateObject(_contract,ContractBlob,ContractSize);
                }
                return _contract;
            }
            set
            {
                _contract = value;
                ContractBlob = CompressionHelper.SerializeObject(value, out int outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        [NotMapped]
        public List<SVR_AniDB_Anime> Anime
        {
            get
            {
                List<SVR_AniDB_Anime> relAnime = new List<SVR_AniDB_Anime>();
                foreach (SVR_AnimeSeries serie in GetSeries())
                {
                    SVR_AniDB_Anime anime = serie.GetAnime();
                    if (anime != null) relAnime.Add(anime);
                }

                return relAnime;
            }
        }

        public CL_AnimeGroup_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null)
        {
            if (Contract == null)
                return new CL_AnimeGroup_User(new SeasonComparator());
            CL_AnimeGroup_User contract = Contract.DeepCopy();
            SVR_AnimeGroup_User rr = GetUserRecord(userid);
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

        private SVR_AnimeGroup_User GetOrCreateUserRecord(int userid)
        {
            SVR_AnimeGroup_User rr = GetUserRecord(userid);
            if (rr != null)
                return rr;
            rr = new SVR_AnimeGroup_User(userid, AnimeGroupID)
            {
                using (var upd = Repo.AnimeGroup_User.BeginAdd())
                {
                    upd.Entity.WatchedCount = 0;
                    upd.Entity.UnwatchedEpisodeCount = 0;
                    upd.Entity.PlayedCount = 0;
                    upd.Entity.StoppedCount = 0;
                    upd.Entity.WatchedEpisodeCount = 0;
                    upd.Entity.WatchedDate = null;
                    rr = upd.Commit();
                }
            }

            return rr;
        }

        public static bool IsRelationTypeInExclusions(string type)
        {
            string[] list = ServerSettings.AutoGroupSeriesRelationExclusions.Split('|');
            return list.Any(a => a.Equals(type, StringComparison.InvariantCultureIgnoreCase));
        }

        public SVR_AnimeGroup_User GetUserRecord(int userID)
        {
            return RepoFactory.AnimeGroup_User.GetByUserAndGroupID(userID, AnimeGroupID);
        }

        public bool HasCustomName()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                var groupingCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings(session.Wrap());
                return HasCustomName(groupingCalculator);
            }
        }

        /// <summary>
        /// This checks all related anime by settings for the GroupName. It will likely be slow
        /// </summary>
        /// <returns></returns>
        public bool HasCustomName(AutoAnimeGroupCalculator groupingCalculator)
        {
            int animeID = GetSeries().FirstOrDefault()?.GetAnime().AnimeID ?? 0;
            if (animeID == 0) return false;

            var animes = groupingCalculator.GetIdsOfAnimeInSameGroup(animeID)
                .Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a)).Where(a => a != null)
                .SelectMany(a => a.GetAllTitles()).ToHashSet();
            return !animes.Contains(GroupName);
        }

        /// <summary>
        ///     Renames all Anime groups based on the user's language preferences
        /// </summary>
        public static void RenameAllGroups()
        {
            logger.Info("Starting RenameAllGroups");
            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                var groupingCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings(session.Wrap());
                foreach (SVR_AnimeGroup grp in RepoFactory.AnimeGroup.GetAll().ToList())
                {
                    List<SVR_AnimeSeries> list = grp.GetSeries();

                    // rename the group if it only has one direct child Anime Series
                    if (list.Count == 1)
                    {
                        string newTitle = list[0].GetSeriesName();
                        grp.GroupName = newTitle;
                        grp.SortName = newTitle;
                        RepoFactory.AnimeGroup.Save(grp, true, true);
                    }
                    else if (list.Count > 1)
                    {
                        #region Naming

                        SVR_AnimeSeries series = null;
                        bool hasCustomName = grp.HasCustomName(groupingCalculator);
                        if (grp.DefaultAnimeSeriesID.HasValue)
                        {
                            series = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value);
                            if (series == null)
                                grp.DefaultAnimeSeriesID = null;
                        }

                        if (!grp.DefaultAnimeSeriesID.HasValue)
                            foreach (SVR_AnimeSeries ser in list)
                            {
                                if (ser == null) continue;
                                // Check all titles for custom naming, in case user changed language preferences
                                if (ser.SeriesNameOverride.Equals(grp.GroupName))
                                {
                                    hasCustomName = false;
                                }
                                else
                                {
                                    if (hasCustomName)
                                    {
                                        #region tvdb names

                                        List<TvDB_Series> tvdbs = ser.GetTvDBSeries();
                                        if (tvdbs != null && tvdbs.Count != 0)
                                            if (tvdbs.Any(tvdbser => tvdbser.SeriesName.Equals(grp.GroupName)))
                                                hasCustomName = false;

                                        #endregion
                                    }

                                    if (series == null)
                                    {
                                        series = ser;
                                        continue;
                                    }

                                    if (ser.AirDate < series.AirDate) series = ser;
                                }
                            }

                        if (series != null)
                        {
                            string newTitle = series.GetSeriesName();
                            if (grp.DefaultAnimeSeriesID.HasValue &&
                                grp.DefaultAnimeSeriesID.Value != series.AnimeSeriesID)
                                newTitle = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value)
                                    .GetSeriesName();
                            if (hasCustomName) newTitle = grp.GroupName;
                            // reset tags, description, etc to new series
                            grp.Populate(series);
                            grp.GroupName = newTitle;
                            grp.SortName = newTitle;
                            RepoFactory.AnimeGroup.Save(grp, true, true);
                            grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
                        }

                        #endregion
                    }
                }
            }
            logger.Info("Finished RenameAllGroups");
        }


        public List<SVR_AniDB_Anime> Anime =>
            GetSeries().Select(serie => serie.GetAnime()).Where(anime => anime != null).ToList();

        public decimal AniDBRating
        {
            get
            {
                try
                {
                    decimal totalRating = 0;
                    int totalVotes = 0;

                        upd.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
                    }

                    if (totalVotes == 0)
                        return 0;
                    return totalRating / totalVotes;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in  AniDBRating: {ex}");
                    return 0;
                }
            }
        }

        public List<SVR_AnimeGroup> GetChildGroups()
        {
            return RepoFactory.AnimeGroup.GetByParentID(AnimeGroupID);
        }

        public List<SVR_AnimeGroup> GetAllChildGroups()
        {
            List<SVR_AnimeGroup> grpList = new List<SVR_AnimeGroup>();
            GetAnimeGroupsRecursive(AnimeGroupID, ref grpList);
            return grpList;
        }

        public List<SVR_AnimeSeries> GetSeries()
        {
            List<SVR_AnimeSeries> seriesList = RepoFactory.AnimeSeries.GetByGroupID(AnimeGroupID);
            // Make everything that relies on GetSeries[0] have the proper result
            seriesList = seriesList.OrderBy(a => a.Year).ThenBy(a => a.AirDate).ToList();
            if (!DefaultAnimeSeriesID.HasValue) return seriesList;
            SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(DefaultAnimeSeriesID.Value);
            if (series == null) return seriesList;
            seriesList.Remove(series);
            seriesList.Insert(0, series);
            return seriesList;
        }

        public List<SVR_AnimeSeries> GetAllSeries(bool skipSorting = false)
        {
            List<SVR_AnimeSeries> seriesList = new List<SVR_AnimeSeries>();
            GetAnimeSeriesRecursive(AnimeGroupID, ref seriesList);
            if (skipSorting) return seriesList;

            return seriesList.OrderBy(a => a.Contract?.AniDBAnime?.AniDBAnime?.BeginYear ?? int.Parse(a.Year.Split('-')[0])).ThenBy(a => a.AirDate).ToList();
        }

        public static Dictionary<int, GroupVotes> BatchGetVotes(IReadOnlyCollection<SVR_AnimeGroup> animeGroups)
        {
            if (animeGroups == null)
                throw new ArgumentNullException(nameof(animeGroups));

            var votesByGroup = new Dictionary<int, GroupVotes>();

            if (animeGroups.Count == 0)
                return votesByGroup;

            var seriesByGroup = animeGroups.ToDictionary(g => g.AnimeGroupID, g => g.GetAllSeries());
            var allAnimeIds = seriesByGroup.Values.SelectMany(serLst => serLst.Select(series => series.AniDB_ID))
                .ToArray();
            var votesByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(allAnimeIds);

            foreach (SVR_AnimeGroup animeGroup in animeGroups)
            {
                decimal allVoteTotal = 0m;
                decimal permVoteTotal = 0m;
                decimal tempVoteTotal = 0m;
                int allVoteCount = 0;
                int permVoteCount = 0;
                int tempVoteCount = 0;
                var groupSeries = seriesByGroup[animeGroup.AnimeGroupID];

                foreach (SVR_AnimeSeries series in groupSeries)
                    if (votesByAnime.TryGetValue(series.AniDB_ID, out AniDB_Vote vote))
                    {
                        allVoteCount++;
                        allVoteTotal += vote.VoteValue;

                        switch (vote.VoteType)
                        {
                            case (int)AniDBVoteType.Anime:
                                permVoteCount++;
                                permVoteTotal += vote.VoteValue;
                                break;
                            case (int)AniDBVoteType.AnimeTemp:
                                tempVoteCount++;
                                tempVoteTotal += vote.VoteValue;
                                break;
                        }
                    }

                var groupVotes = new GroupVotes(allVoteCount == 0 ? (decimal?) null : allVoteTotal / allVoteCount / 100m, permVoteCount == 0 ? (decimal?) null : permVoteTotal / permVoteCount / 100m, tempVoteCount == 0 ? (decimal?) null : tempVoteTotal / tempVoteCount / 100m);

                votesByGroup[animeGroup.AnimeGroupID] = groupVotes;
            }

            return votesByGroup;
        }

        public List<AniDB_Tag> Tags
        {
            get
            {
                List<int> animeTagIDs = new List<int>();
                List<AniDB_Anime_Tag> animeTags = new List<AniDB_Anime_Tag>();

                foreach (SVR_AnimeSeries ser in GetAllSeries())
                foreach (AniDB_Anime_Tag aac in ser.GetAnime().GetAnimeTags())
                    if (!animeTagIDs.Contains(aac.AniDB_Anime_TagID))
                    {
                        animeTagIDs.Add(aac.AniDB_Anime_TagID);
                        animeTags.Add(aac);
                    }

                return animeTags.OrderByDescending(a => a.Weight)
                    .Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID)).Where(tag => tag != null)
                    .ToList();
            }
        }

        public List<CustomTag> CustomTags
        {
            get
            {
                List<CustomTag> tags = new List<CustomTag>();
                List<int> tagIDs = new List<int>();


                // get a list of all the unique custom tags for all the series in this group
                foreach (SVR_AnimeSeries ser in GetAllSeries())
                foreach (CustomTag tag in RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID))
                    if (!tagIDs.Contains(tag.CustomTagID))
                    {
                        tagIDs.Add(tag.CustomTagID);
                        tags.Add(tag);
                    }

                return tags.OrderBy(a => a.TagName).ToList();
            }
        }

        public List<AniDB_Anime_Title> Titles
        {
            get
            {
                List<int> animeTitleIDs = new List<int>();
                List<AniDB_Anime_Title> animeTitles = new List<AniDB_Anime_Title>();


                // get a list of all the unique titles for this all the series in this group
                foreach (SVR_AnimeSeries ser in GetAllSeries())
                foreach (AniDB_Anime_Title aat in ser.GetAnime().GetTitles())
                    if (!animeTitleIDs.Contains(aat.AniDB_Anime_TitleID))
                    {
                        animeTitleIDs.Add(aat.AniDB_Anime_TitleID);
                        animeTitles.Add(aat);
                    }

                return animeTitles;
            }
        }

        public override string ToString()
        {
            return $"Group: {GroupName} ({AnimeGroupID})";
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
                foreach (SVR_AnimeSeries ser in GetAllSeries())
                    ser.UpdateStats(watchedStats, missingEpsStats, false);

            // now recursively update stats for all the child groups
            // and update the stats for the groups
            foreach (SVR_AnimeGroup grp in GetAllChildGroups())
                grp.UpdateStats(watchedStats, missingEpsStats);

            UpdateStats(this, watchedStats, missingEpsStats);
        }

        /// <summary>
        ///     Update the stats for this group based on the child series
        ///     Assumes that all the AnimeSeries have had their stats updated already
        /// </summary>
        public static void UpdateStats(SVR_AnimeGroup grp, bool watchedStats, bool missingEpsStats)
        {
            List<SVR_AnimeSeries> seriesList = grp.GetAllSeries();

            if (missingEpsStats)
            {
                using (var upd = Repo.AnimeGroup.BeginAddOrUpdate(()=>Repo.AnimeGroup.GetByID(grp.AnimeGroupID)))
                {
                    UpdateMissingEpisodeStats_RA(upd.Entity, seriesList);
                    grp=upd.Commit((true, false, false));
                }
            }

            if (watchedStats)
            {
                using (IBatchAtomic<SVR_AnimeGroup_User, object> grpusers = Repo.AnimeGroup_User.BeginBatchUpdate(()=>Repo.AnimeGroup_User.GetByGroupID(grp.AnimeGroupID)))
                {
                    // Now update the stats for the groups
                    logger.Trace("Updating stats for {0}", ToString());
                    RepoFactory.AnimeGroup_User.Save(userRecord);
                });
            }
        }
        public static IEnumerable<SVR_AnimeGroup_User> BatchUpdateStats(IEnumerable<SVR_AnimeGroup> animeGroups, bool watchedStats = true, bool missingEpsStats = true)
        {
            if (animeGroups == null)
                throw new ArgumentNullException(nameof(animeGroups));

            if (!watchedStats && !missingEpsStats)
                return; // Nothing to do

         
            List<int> users = Repo.JMMUser.GetIds();
            List<SVR_AnimeGroup_User> gusers=new List<SVR_AnimeGroup_User>();
            foreach (SVR_AnimeGroup animeGroup in animeGroups)
            {
                List<SVR_AnimeSeries> animeSeries = animeGroup.GetAllSeries();
                SVR_AnimeGroup grp = animeGroup;
                if (missingEpsStats)
                    UpdateMissingEpisodeStats(animeGroup, animeSeries);
                if (watchedStats)
                    UpdateWatchedStats(animeGroup, animeSeries, allUsers.Value, (userRecord, isNew) =>
                    {
                        if (isNew)
                            createdGroupUsers?.Add(userRecord);
                        else
                            updatedGroupUsers?.Add(userRecord);
                    });
            }

            return gusers;
        }

        /*
        /// <summary>
        /// Updates the watched stats for the specified anime group.
        /// </summary>
        /// <param name="animeGroup_ra">The <see cref="SVR_AnimeGroup"/> that is to have it's watched stats updated.</param>
        /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> that belong to <paramref name="animeGroup_ra"/>.</param>
        /// <param name="allUsers">A sequence of all JMM users.</param>
        /// <param name="newAnimeGroupUsers">A methed that will be called for each processed <see cref="SVR_AnimeGroup_User"/>
        /// and whether or not the <see cref="SVR_AnimeGroup_User"/> is new.</param>
        private static void UpdateWatchedStats(SVR_AnimeGroup animeGroup_ra, 
            IEnumerable<SVR_AnimeSeries> seriesList,
            IEnumerable<SVR_JMMUser> allUsers, Action<SVR_AnimeGroup_User, bool> newAnimeGroupUsers)
        {
            foreach (SVR_JMMUser juser in allUsers)
            {
                SVR_AnimeGroup_User userRecord = animeGroup_ra.GetUserRecord(juser.JMMUserID);
                bool isNewRecord = false;

                if (userRecord == null)
                {
                    userRecord = new SVR_AnimeGroup_User(juser.JMMUserID, animeGroup_ra.AnimeGroupID);
                    isNewRecord = true;
                }

                // Reset stats
                userRecord.WatchedCount = 0;
                userRecord.UnwatchedEpisodeCount = 0;
                userRecord.PlayedCount = 0;
                userRecord.StoppedCount = 0;
                userRecord.WatchedEpisodeCount = 0;
                userRecord.WatchedDate = null;

                foreach (SVR_AnimeSeries ser in seriesList)
                {
                    SVR_AnimeSeries_User serUserRecord = ser.GetUserRecord(juser.JMMUserID);

                    if (serUserRecord != null)
                    {
                        userRecord.WatchedCount += serUserRecord.WatchedCount;
                        userRecord.UnwatchedEpisodeCount += serUserRecord.UnwatchedEpisodeCount;
                        userRecord.PlayedCount += serUserRecord.PlayedCount;
                        userRecord.StoppedCount += serUserRecord.StoppedCount;
                        userRecord.WatchedEpisodeCount += serUserRecord.WatchedEpisodeCount;

                        if (serUserRecord.WatchedDate != null
                            && (userRecord.WatchedDate == null || serUserRecord.WatchedDate > userRecord.WatchedDate))
                            userRecord.WatchedDate = serUserRecord.WatchedDate;
                    }
                }

                newAnimeGroupUsers(userRecord, isNewRecord);
            }
        }
        */

        private static void UpdateWatchedStats_RA(SVR_AnimeGroup_User groupuser_ra, IEnumerable<SVR_AnimeSeries> seriesList)
        {
            // Reset stats
            groupuser_ra.WatchedCount = 0;
            groupuser_ra.UnwatchedEpisodeCount = 0;
            groupuser_ra.PlayedCount = 0;
            groupuser_ra.StoppedCount = 0;
            groupuser_ra.WatchedEpisodeCount = 0;
            groupuser_ra.WatchedDate = null;


            foreach (SVR_AnimeSeries ser in seriesList)
            {
                SVR_AnimeSeries_User serUserRecord = ser.GetUserRecord(groupuser_ra.JMMUserID);

                if (serUserRecord != null)
                {
                    groupuser_ra.WatchedCount += serUserRecord.WatchedCount;
                    groupuser_ra.UnwatchedEpisodeCount += serUserRecord.UnwatchedEpisodeCount;
                    groupuser_ra.PlayedCount += serUserRecord.PlayedCount;
                    groupuser_ra.StoppedCount += serUserRecord.StoppedCount;
                    groupuser_ra.WatchedEpisodeCount += serUserRecord.WatchedEpisodeCount;
                    if (serUserRecord.WatchedDate != null && (groupuser_ra.WatchedDate == null || serUserRecord.WatchedDate > groupuser_ra.WatchedDate))
                    {
                        groupuser_ra.WatchedDate = serUserRecord.WatchedDate;
                    }
                }
            }
        }

        /// <summary>
        ///     Updates the missing episode stats for the specified anime group.
        /// </summary>
        /// <remarks>
        ///     NOTE: This method does NOT save the changes made to the database.
        ///     NOTE 2: Assumes that all the AnimeSeries have had their stats updated already.
        /// </remarks>
        /// <param name="animeGroup_ra">The <see cref="SVR_AnimeGroup" /> that is to have it's missing episode stats updated.</param>
        /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries" /> that belong to <paramref name="animeGroup_ra" />.</param>
        private static void UpdateMissingEpisodeStats_RA(SVR_AnimeGroup animeGroup_ra, IEnumerable<SVR_AnimeSeries> seriesList)
        {
            animeGroup_ra.MissingEpisodeCount = 0;
            animeGroup_ra.MissingEpisodeCountGroups = 0;

            foreach (SVR_AnimeSeries series in seriesList)
            {
                animeGroup_ra.MissingEpisodeCount += series.MissingEpisodeCount;
                animeGroup_ra.MissingEpisodeCountGroups += series.MissingEpisodeCountGroups;

                // Now series.LatestEpisodeAirDate should never be greater than today
                if (series.LatestEpisodeAirDate.HasValue)
                    if ((animeGroup.LatestEpisodeAirDate.HasValue && series.LatestEpisodeAirDate.Value >
                         animeGroup.LatestEpisodeAirDate.Value)
                        || !animeGroup.LatestEpisodeAirDate.HasValue)
                        animeGroup.LatestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
        }


        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeGroup_User oldcontract, CL_AnimeGroup_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();
            if (oldcontract == null || oldcontract.Stat_IsComplete != newcontract.Stat_IsComplete)
                h.Add(GroupFilterConditionType.CompletedSeries);
            if (oldcontract == null || (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) != (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
                h.Add(GroupFilterConditionType.MissingEpisodes);
            if (oldcontract == null || !oldcontract.Stat_AllTags.SetEquals(newcontract.Stat_AllTags))
                h.Add(GroupFilterConditionType.Tag);
            if (oldcontract == null || oldcontract.Stat_AirDate_Min != newcontract.Stat_AirDate_Min || oldcontract.Stat_AirDate_Max != newcontract.Stat_AirDate_Max)
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
            if (oldcontract == null || !oldcontract.Stat_AllVideoQuality.SetEquals(newcontract.Stat_AllVideoQuality) || !oldcontract.Stat_AllVideoQuality_Episodes.SetEquals(newcontract.Stat_AllVideoQuality_Episodes))
                h.Add(GroupFilterConditionType.VideoQuality);
            if (oldcontract == null || oldcontract.AnimeGroupID != newcontract.AnimeGroupID)
                h.Add(GroupFilterConditionType.AnimeGroup);
            if (oldcontract == null || oldcontract.Stat_AniDBRating != newcontract.Stat_AniDBRating)
                h.Add(GroupFilterConditionType.AniDBRating);
            if (oldcontract == null || oldcontract.Stat_SeriesCreatedDate != newcontract.Stat_SeriesCreatedDate)
                h.Add(GroupFilterConditionType.SeriesCreatedDate);
            if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
                h.Add(GroupFilterConditionType.EpisodeAddedDate);
            if (oldcontract == null || oldcontract.Stat_HasFinishedAiring != newcontract.Stat_HasFinishedAiring || oldcontract.Stat_IsCurrentlyAiring != newcontract.Stat_IsCurrentlyAiring)
                h.Add(GroupFilterConditionType.FinishedAiring);
            if (oldcontract == null || oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
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
            if (oldcontract?.Stat_AllYears == null || !oldcontract.Stat_AllYears.SetEquals(newcontract.Stat_AllYears))
                h.Add(GroupFilterConditionType.Year);

            if (oldcontract?.Stat_AllSeasons == null || !oldcontract.Stat_AllSeasons.SetEquals(newcontract.Stat_AllSeasons))
                h.Add(GroupFilterConditionType.Season);

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

        public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(List<SVR_AnimeGroup> animeGroups, bool updateStats)
        {
            if (animeGroups == null)
                throw new ArgumentNullException(nameof(animeGroups));

            var grpFilterCondTypesByGroup = new Dictionary<int, HashSet<GroupFilterConditionType>>();

            if (animeGroups.Count == 0)
                return grpFilterCondTypesByGroup;

            var seriesByGroup = animeGroups.ToDictionary(g => g.AnimeGroupID, g => g.GetAllSeries());
            var allAnimeIds = new Lazy<int[]>(
                () => seriesByGroup.Values.SelectMany(serLst => serLst.Select(series => series.AniDB_ID)).ToArray(),
                isThreadSafe: false);
            var allGroupIds = new Lazy<int[]>(
                () => animeGroups.Select(grp => grp.AnimeGroupID).ToArray(), isThreadSafe: false);
            var audioLangStatsByAnime = new Lazy<Dictionary<int, LanguageStat>>(
                () => RepoFactory.Adhoc.GetAudioLanguageStatsByAnime(session, allAnimeIds.Value), isThreadSafe: false);
            var subLangStatsByAnime = new Lazy<Dictionary<int, LanguageStat>>(
                () => RepoFactory.Adhoc.GetSubtitleLanguageStatsByAnime(session, allAnimeIds.Value),
                isThreadSafe: false);
            var tvDbXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_TvDB>>(
                () => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeIDs(allAnimeIds.Value), isThreadSafe: false);
            var allVidQualByGroup = new Lazy<ILookup<int, string>>(
                () => RepoFactory.Adhoc.GetAllVideoQualityByGroup(session, allGroupIds.Value), isThreadSafe: false);
            var movieDbXRefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_Other>>(
                () => RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDsAndType(session, allAnimeIds.Value,
                    CrossRefType.MovieDB), isThreadSafe: false);
            var malXRefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_MAL>>(
                () => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeIDs(session, allAnimeIds.Value), isThreadSafe: false);
            var votesByGroup = BatchGetVotes(animeGroups);
            DateTime now = DateTime.Now;
            Dictionary<int, CL_AnimeGroup_User> contracts=new Dictionary<int, CL_AnimeGroup_User>();

            foreach (SVR_AnimeGroup animeGroup in animeGroups)
            {
                CL_AnimeGroup_User contract = animeGroup.Contract?.DeepCopy();
                bool localUpdateStats = updateStats;

                if (contract == null)
                {
                    contract = new CL_AnimeGroup_User();
                    localUpdateStats = true;
                }

                contract.AnimeGroupID = animeGroup.AnimeGroupID;
                contract.AnimeGroupParentID = animeGroup.AnimeGroupParentID;
                contract.DefaultAnimeSeriesID = animeGroup.DefaultAnimeSeriesID;
                contract.GroupName = animeGroup.GroupName;
                contract.Description = animeGroup.Description;
                contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;
                contract.SortName = animeGroup.SortName;
                contract.EpisodeAddedDate = animeGroup.EpisodeAddedDate;
                contract.OverrideDescription = animeGroup.OverrideDescription;
                contract.DateTimeUpdated = animeGroup.DateTimeUpdated;
                contract.IsFave = 0;
                contract.UnwatchedEpisodeCount = 0;
                contract.WatchedEpisodeCount = 0;
                contract.WatchedDate = null;
                contract.PlayedCount = 0;
                contract.WatchedCount = 0;
                contract.StoppedCount = 0;
                contract.MissingEpisodeCount = animeGroup.MissingEpisodeCount;
                contract.MissingEpisodeCountGroups = animeGroup.MissingEpisodeCountGroups;

                List<SVR_AnimeSeries> allSeriesForGroup = seriesByGroup[animeGroup.AnimeGroupID];

                if (localUpdateStats)
                {
                    DateTime? airDateMin = null;
                    DateTime? airDateMax = null;
                    DateTime? groupEndDate = new DateTime(1980, 1, 1);
                    DateTime? seriesCreatedDate = null;
                    bool isComplete = false;
                    bool hasFinishedAiring = false;
                    bool isCurrentlyAiring = false;
                    HashSet<string> videoQualityEpisodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    HashSet<string> audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    HashSet<string> subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    // Even though the contract value says 'has link', it's easier to think about whether it's missing
                    bool missingTvDBLink = false;
                    bool missingMALLink = false;
                    bool missingMovieDBLink = false;
                    bool missingTvDBAndMovieDBLink = false;
                    int seriesCount = 0;
                    int epCount = 0;

                    HashSet<int> allYears = new HashSet<int>();
                    SortedSet<string> allSeasons = new SortedSet<string>(new SeasonComparator());

                    foreach (SVR_AnimeSeries series in allSeriesForGroup)
                    {
                        seriesCount++;

                        List<SVR_VideoLocal> vidsTemp = Repo.VideoLocal.GetByAniDBAnimeID(series.AniDB_ID);
                        List<CrossRef_File_Episode> crossRefs = Repo.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID);
                        ILookup<int, CrossRef_File_Episode> crossRefsLookup = crossRefs.ToLookup(cr => cr.EpisodeID);
                        var dictVids = new Dictionary<string, SVR_VideoLocal>();

                        foreach (SVR_VideoLocal vid in vidsTemp)
                        //Hashes may be repeated from multiple locations but we don't care
                            dictVids[vid.Hash] = vid;

                        // All Video Quality Episodes
                        // Try to determine if this anime has all the episodes available at a certain video quality
                        // e.g.  the series has all episodes in blu-ray
                        // Also look at languages
                        Dictionary<string, int> vidQualEpCounts = new Dictionary<string, int>();
                        // video quality, count of episodes
                        SVR_AniDB_Anime anime = series.GetAnime();

                        foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                        {
                            if (ep.EpisodeTypeEnum != EpisodeType.Episode)
                                continue;

                            var epVids = new List<SVR_VideoLocal>();

                            foreach (CrossRef_File_Episode xref in crossRefsLookup[ep.AniDB_EpisodeID])
                            {
                                if (xref.EpisodeID != ep.AniDB_EpisodeID)
                                    continue;


                                if (dictVids.TryGetValue(xref.Hash, out SVR_VideoLocal video))
                                    epVids.Add(video);
                            }

                            var qualityAddedSoFar = new HashSet<string>();

                            // Handle mutliple files of the same quality for one episode
                            foreach (SVR_VideoLocal vid in epVids)
                            {
                                SVR_AniDB_File anifile = vid.GetAniDBFile();

                                if (anifile == null)
                                    continue;

                                if (!qualityAddedSoFar.Contains(anifile.File_Source))
                                {
                                    vidQualEpCounts.TryGetValue(anifile.File_Source, out int srcCount);
                                    vidQualEpCounts[anifile.File_Source] = srcCount + 1; // If the file source wasn't originally in the dictionary, then it will be set to 1

                                    qualityAddedSoFar.Add(anifile.File_Source);
                                }
                            }
                        }

                        epCount += anime.EpisodeCountNormal;

                        // Add all video qualities that span all of the normal episodes
                        videoQualityEpisodes.UnionWith(vidQualEpCounts.Where(vqec => anime.EpisodeCountNormal == vqec.Value).Select(vqec => vqec.Key));


                        // Audio languages
                        if (audioLangStatsByAnime.Value.TryGetValue(anime.AnimeID, out LanguageStat langStats))
                            audioLanguages.UnionWith(langStats.LanguageNames);

                        // Subtitle languages
                        if (subLangStatsByAnime.Value.TryGetValue(anime.AnimeID, out langStats))
                            subtitleLanguages.UnionWith(langStats.LanguageNames);

                        // Calculate Air Date
                        DateTime seriesAirDate = series.AirDate;

                        if (seriesAirDate != DateTime.MinValue)
                        {
                            if (airDateMin == null || seriesAirDate < airDateMin.Value)
                                airDateMin = seriesAirDate;

                            if (airDateMax == null || seriesAirDate > airDateMax.Value)
                                airDateMax = seriesAirDate;
                        }

                        // Calculate end date
                        // If the end date is NULL it actually means it is ongoing, so this is the max possible value
                        DateTime? seriesEndDate = series.EndDate;

                        if (seriesEndDate == null || groupEndDate == null)
                            groupEndDate = null;
                        else if (seriesEndDate.Value > groupEndDate.Value)
                            groupEndDate = seriesEndDate;

                        // Note - only one series has to be finished airing to qualify
                        if (series.EndDate != null && series.EndDate.Value < now)
                            hasFinishedAiring = true;
                        // Note - only one series has to be finished airing to qualify
                        if (series.EndDate == null || series.EndDate.Value > now)
                            isCurrentlyAiring = true;

                        // We evaluate IsComplete as true if
                        // 1. series has finished airing
                        // 2. user has all episodes locally
                        // Note - only one series has to be complete for the group to be considered complete
                        if (series.EndDate != null && series.EndDate.Value < now
                            && series.MissingEpisodeCount == 0 && series.MissingEpisodeCountGroups == 0)
                            isComplete = true;

                        // Calculate Series Created Date
                        DateTime createdDate = series.DateTimeCreated;

                        if (seriesCreatedDate == null || createdDate < seriesCreatedDate.Value)
                            seriesCreatedDate = createdDate;

                        // For the group, if any of the series don't have a tvdb link
                        // we will consider the group as not having a tvdb link
                        bool foundTvDBLink = tvDbXrefByAnime.Value[anime.AnimeID].Any();
                        bool foundMovieDBLink = movieDbXRefByAnime.Value[anime.AnimeID].Any();
                        bool isMovie = anime.AnimeType == (int) AnimeType.Movie;
                        if (!foundTvDBLink)
                            if (!isMovie && !(anime.Restricted > 0))
                                missingTvDBLink = true;
                        if (!foundMovieDBLink)
                            if (isMovie && !(anime.Restricted > 0))
                                missingMovieDBLink = true;
                        if (!malXRefByAnime.Value[anime.AnimeID].Any())
                            missingMALLink = true;

                        missingTvDBAndMovieDBLink |= !(anime.Restricted > 0) && !foundTvDBLink && !foundMovieDBLink;

                        int endyear = anime.EndYear;
                        if (endyear == 0) endyear = DateTime.Today.Year;
                        if (anime.BeginYear != 0)
                        {
                            var years = Enumerable.Range(anime.BeginYear, endyear - anime.BeginYear + 1).Where(anime.IsInYear).ToList();
                            allYears.UnionWith(years);
                            foreach (int year in years)
                            foreach (AnimeSeason season in Enum.GetValues(typeof(AnimeSeason)))
                                if (anime.IsInSeason(season, year)) allSeasons.Add($"{season} {year}");
                        }
                    }

                    contract.Stat_AllYears = allYears;
                    contract.Stat_AllSeasons = allSeasons;
                    contract.Stat_AllTags = animeGroup.Tags
                        .Select(a => a.TagName.Trim())
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    contract.Stat_AllCustomTags = animeGroup.CustomTags
                        .Select(a => a.TagName)
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    contract.Stat_AllTitles = animeGroup.Titles
                        .Select(a => a.Title)
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    contract.Stat_AnimeTypes = allSeriesForGroup
                        .Select(a => a.Contract?.AniDBAnime?.AniDBAnime)
                        .Where(a => a != null)
                        .Select(a => a.AnimeType.ToString())
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    contract.Stat_AllVideoQuality = allVidQualByGroup.Value[animeGroup.AnimeGroupID]
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    contract.Stat_IsComplete = isComplete;
                    contract.Stat_HasFinishedAiring = hasFinishedAiring;
                    contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
                    contract.Stat_HasTvDBLink = !missingTvDBLink; // Has a link if it isn't missing
                    contract.Stat_HasMALLink = !missingMALLink; // Has a link if it isn't missing
                    contract.Stat_HasMovieDBLink = !missingMovieDBLink; // Has a link if it isn't missing
                    contract.Stat_HasMovieDBOrTvDBLink = !missingTvDBAndMovieDBLink; // Has a link if it isn't missing
                    contract.Stat_SeriesCount = seriesCount;
                    contract.Stat_EpisodeCount = epCount;
                    contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
                    contract.Stat_AirDate_Min = airDateMin;
                    contract.Stat_AirDate_Max = airDateMax;
                    contract.Stat_EndDate = groupEndDate;
                    contract.Stat_SeriesCreatedDate = seriesCreatedDate;
                    contract.Stat_AniDBRating = animeGroup.AniDBRating;
                    contract.Stat_AudioLanguages = audioLanguages;
                    contract.Stat_SubtitleLanguages = subtitleLanguages;
                    contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;


                    votesByGroup.TryGetValue(animeGroup.AnimeGroupID, out GroupVotes votes);
                    contract.Stat_UserVoteOverall = votes?.AllVotes;
                    contract.Stat_UserVotePermanent = votes?.PermanentVotes;
                    contract.Stat_UserVoteTemporary = votes?.TemporaryVotes;
                }

                grpFilterCondTypesByGroup[animeGroup.AnimeGroupID] = GetConditionTypesChanged(animeGroup.Contract, contract);
                contracts.Add(animeGroup.AnimeGroupID, contract);
            }





            using (var gupdate = Repo.AnimeGroup.BeginBatchUpdate(() => Repo.AnimeGroup.GetMany(contracts.Keys)))
            {
                foreach (SVR_AnimeGroup animeGroup in gupdate)
                {
                    animeGroup.Contract = contracts[animeGroup.AnimeGroupID];
                    gupdate.Update(animeGroup);
                }

                gupdate.Commit();
            }

            return grpFilterCondTypesByGroup;
        }

        public HashSet<GroupFilterConditionType> UpdateContract(bool updatestats)
        {
            var grpFilterCondTypesByGroup = BatchUpdateContracts(new List<SVR_AnimeGroup> { this }, updatestats);

            return grpFilterCondTypesByGroup[AnimeGroupID];
        }

        public void DeleteFromFilters()
        {
            using (var upd = Repo.GroupFilter.BeginBatchUpdate(() => Repo.GroupFilter.GetAll()))
            {
                bool change = false;
                foreach (int k in gf.GroupsIds.Keys)
                    if (gf.GroupsIds[k].Contains(AnimeGroupID))
                    {
                        foreach (int k in gf.GroupsIds.Keys)
                        {
                            if (gf.GroupsIds[k].Contains(AnimeGroupID))
                                gf.GroupsIds[k].Remove(AnimeGroupID);
                        }
                        upd.Update(gf);
                    }
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }

        public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, SVR_JMMUser user = null)
        {
            HashSet<GroupFilterConditionType> n = new HashSet<GroupFilterConditionType>(types);
            IEnumerable<SVR_JMMUser> users = new List<SVR_JMMUser> {user};
            if (user == null)
                users = RepoFactory.JMMUser.GetAll();
            List<SVR_GroupFilter> tosave = new List<SVR_GroupFilter>();

            HashSet<GroupFilterConditionType> n = new HashSet<GroupFilterConditionType>(types);
            foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetWithConditionTypesAndAll(n))
            {
                if (gf.UpdateGroupFilterFromGroup(Contract, null))
                    if (!tosave.Contains(gf))
                        tosave.Add(gf);
                foreach (SVR_JMMUser u in users)
                {
                    CL_AnimeGroup_User cgrp = GetUserContract(u.JMMUserID, n);

                    if (gf.UpdateGroupFilterFromGroup(cgrp, u))
                        if (!tosave.Contains(gf))
                            tosave.Add(gf);
                }
            }

            RepoFactory.GroupFilter.Save(tosave);
        }

        public static void GetAnimeGroupsRecursive(int animeGroupID, ref List<SVR_AnimeGroup> groupList)
        {
            SVR_AnimeGroup grp = Repo.AnimeGroup.GetByID(animeGroupID);
            if (grp == null) return;

            // get the child groups for this group
            groupList.AddRange(grp.GetChildGroups());

            foreach (SVR_AnimeGroup childGroup in grp.GetChildGroups())
                GetAnimeGroupsRecursive(childGroup.AnimeGroupID, ref groupList);
        }

        public static void GetAnimeSeriesRecursive(int animeGroupID, ref List<SVR_AnimeSeries> seriesList)
        {
            SVR_AnimeGroup grp = Repo.AnimeGroup.GetByID(animeGroupID);
            if (grp == null) return;

            // get the series for this group
            List<SVR_AnimeSeries> thisSeries = grp.GetSeries();
            seriesList.AddRange(thisSeries);

            foreach (SVR_AnimeGroup childGroup in grp.GetChildGroups())
                GetAnimeSeriesRecursive(childGroup.AnimeGroupID, ref seriesList);
        }

        public SVR_AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                if (!AnimeGroupParentID.HasValue) return this;
                SVR_AnimeGroup parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupParentID.Value);
                while (parentGroup?.AnimeGroupParentID != null)
                    parentGroup = RepoFactory.AnimeGroup.GetByID(parentGroup.AnimeGroupParentID.Value);
                return parentGroup;
            }
        }
    }

    public class GroupVotes
    {
        public GroupVotes(decimal? allVotes = null, decimal? permanentVotes = null, decimal? temporaryVotes = null)
        {
            AllVotes = allVotes;
            PermanentVotes = permanentVotes;
            TemporaryVotes = temporaryVotes;
        }

        public decimal? AllVotes { get; }

        public decimal? PermanentVotes { get; }

        public decimal? TemporaryVotes { get; }
    }
}
