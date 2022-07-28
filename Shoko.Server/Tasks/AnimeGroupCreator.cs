using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Tasks
{
    internal class AnimeGroupCreator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private const int DefaultBatchSize = 50;
        public const string TempGroupName = "AAA Migrating Groups AAA";
        private static readonly Regex _truncateYearRegex = new Regex(@"\s*\(\d{4}\)$");
        private readonly AniDB_AnimeRepository _aniDbAnimeRepo = RepoFactory.AniDB_Anime;
        private readonly AnimeSeriesRepository _animeSeriesRepo = RepoFactory.AnimeSeries;
        private readonly AnimeGroupRepository _animeGroupRepo = RepoFactory.AnimeGroup;
        private readonly AnimeGroup_UserRepository _animeGroupUserRepo = RepoFactory.AnimeGroup_User;
        private readonly GroupFilterRepository _groupFilterRepo = RepoFactory.GroupFilter;
        private readonly JMMUserRepository _userRepo = RepoFactory.JMMUser;
        private readonly bool _autoGroupSeries;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimeGroupCreator"/> class.
        /// </summary>
        /// <param name="autoGroupSeries"><c>true</c> to automatically assign to groups based on aniDB relations;
        /// otherwise, <c>false</c> to assign each series to its own group.</param>
        public AnimeGroupCreator(bool autoGroupSeries)
        {
            _autoGroupSeries = autoGroupSeries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimeGroupCreator"/> class.
        /// </summary>
        /// <remarks>
        /// Uses the current server configuration to determine if auto grouping series is enabled.
        /// </remarks>
        public AnimeGroupCreator()
            : this(ServerSettings.Instance.AutoGroupSeries)
        {
        }

        /// <summary>
        /// Creates a new group that series will be put in during group re-calculation.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>The temporary <see cref="SVR_AnimeGroup"/>.</returns>
        private SVR_AnimeGroup CreateTempAnimeGroup(ISessionWrapper session)
        {
            DateTime now = DateTime.Now;

            var tempGroup = new SVR_AnimeGroup
            {
                GroupName = TempGroupName,
                Description = TempGroupName,
                SortName = TempGroupName,
                DateTimeUpdated = now,
                DateTimeCreated = now
            };

            // We won't use AnimeGroupRepository.Save because we don't need to perform all the extra stuff since this is for temporary use only
            session.Insert(tempGroup);
            lock (RepoFactory.AnimeGroup.Cache)
            {
                RepoFactory.AnimeGroup.Cache.Update(tempGroup);
            }

            return tempGroup;
        }

        /// <summary>
        /// Deletes the anime groups and user mappings as well as resetting group filters and moves all anime series into the specified group.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="tempGroupId">The ID of the temporary anime group to use for migration.</param>
        private void ClearGroupsAndDependencies(ISessionWrapper session, int tempGroupId)
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Removing existing AnimeGroups and resetting GroupFilters"};
            _log.Info("Removing existing AnimeGroups and resetting GroupFilters");

            _animeGroupUserRepo.DeleteAll(session);
            _animeGroupRepo.DeleteAll(session, tempGroupId);
            session.CreateSQLQuery(@"
                UPDATE AnimeSeries SET AnimeGroupID = :tempGroupId;
                UPDATE GroupFilter SET GroupsIdsString = '{}';")
                .SetInt32("tempGroupId", tempGroupId)
                .ExecuteUpdate();

            // We've deleted/modified all AnimeSeries/GroupFilter records, so update caches to reflect that
            _animeSeriesRepo.ClearCache();
            _groupFilterRepo.ClearCache();
            _log.Info("AnimeGroups have been removed and GroupFilters have been reset");
        }

        private void UpdateAnimeSeriesContractsAndSave(ISessionWrapper session,
            IReadOnlyCollection<SVR_AnimeSeries> series)
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Updating contracts for AnimeSeries"};
            _log.Info("Updating contracts for AnimeSeries");

            // Update batches of AnimeSeries contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(series.Batch(DefaultBatchSize), new ParallelOptions {MaxDegreeOfParallelism = 4},
                localInit: () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
                body: (seriesBatch, state, localSession) =>
                {
                    SVR_AnimeSeries.BatchUpdateContracts(localSession.Wrap(), seriesBatch);
                    return localSession;
                },
                localFinally: localSession => { localSession.Dispose(); });

            _animeSeriesRepo.UpdateBatch(session, series);
            _log.Info("AnimeSeries contracts have been updated");
        }

        private void UpdateAnimeGroupsAndTheirContracts(ISessionWrapper session,
            IReadOnlyCollection<SVR_AnimeGroup> groups)
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Updating statistics and contracts for AnimeGroups"};
            _log.Info("Updating statistics and contracts for AnimeGroups");

            var allCreatedGroupUsers = new ConcurrentBag<List<SVR_AnimeGroup_User>>();

            // Update batches of AnimeGroup contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(groups.Batch(DefaultBatchSize), new ParallelOptions {MaxDegreeOfParallelism = 4},
                localInit: () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
                body: (groupBatch, state, localSession) =>
                {
                    var createdGroupUsers = new List<SVR_AnimeGroup_User>(groupBatch.Length);

                    // We shouldn't need to keep track of updates to AnimeGroup_Users in the below call, because they should have all been deleted,
                    // therefore they should all be new
                    SVR_AnimeGroup.BatchUpdateStats(groupBatch, watchedStats: true, missingEpsStats: true,
                        createdGroupUsers: createdGroupUsers);
                    allCreatedGroupUsers.Add(createdGroupUsers);
                    SVR_AnimeGroup.BatchUpdateContracts(localSession.Wrap(), groupBatch, updateStats: true);

                    return localSession;
                },
                localFinally: localSession => { localSession.Dispose(); });

            _animeGroupRepo.UpdateBatch(session, groups);
            _log.Info("AnimeGroup statistics and contracts have been updated");

            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Creating AnimeGroup_Users and updating plex/kodi contracts"};
            _log.Info("Creating AnimeGroup_Users and updating plex/kodi contracts");

            List<SVR_AnimeGroup_User> animeGroupUsers = allCreatedGroupUsers.SelectMany(groupUsers => groupUsers)
                .ToList();

            // Insert the AnimeGroup_Users so that they get assigned a primary key before we update plex/kodi contracts
            _animeGroupUserRepo.InsertBatch(session, animeGroupUsers);
            // We need to repopulate caches for AnimeGroup_User and AnimeGroup because we've updated/inserted them
            // and they need to be up to date for the plex/kodi contract updating to work correctly
            _animeGroupUserRepo.Populate(session, displayname: false);
            _animeGroupRepo.Populate(session, displayname: false);

            // NOTE: There are situations in which UpdatePlexKodiContracts will cause database database writes to occur, so we can't
            // use Parallel.ForEach for the time being (If it was guaranteed to only read then we'd be ok)
            foreach (SVR_AnimeGroup_User groupUser in animeGroupUsers)
            {
                groupUser.UpdatePlexKodiContracts(session);
            }

            _animeGroupUserRepo.UpdateBatch(session, animeGroupUsers);
            _log.Info("AnimeGroup_Users have been created");
        }

        /// <summary>
        /// Updates all Group Filters. This should be done as the last step.
        /// </summary>
        /// <remarks>
        /// Assumes that all caches are up to date.
        /// </remarks>
        private void UpdateGroupFilters(ISessionWrapper session)
        {
            _log.Info("Updating Group Filters");
            _log.Info("Calculating Tag Filters");
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Calculating Tag Filters"};
            _groupFilterRepo.CalculateAnimeSeriesPerTagGroupFilter(session);
            _log.Info("Calculating All Other Filters");
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Calculating Non-Tag Filters"};
            IEnumerable<SVR_GroupFilter> grpFilters = _groupFilterRepo.GetAll(session).Where(a =>
                a.FilterType != (int) GroupFilterType.Tag &&
                ((GroupFilterType) a.FilterType & GroupFilterType.Directory) == 0).ToList();

            // The main reason for doing this in parallel is because UpdateEntityReferenceStrings does JSON encoding
            // and is enough work that it can benefit from running in parallel
            Parallel.ForEach(
                grpFilters, filter =>
                {
                    filter.SeriesIds.Clear();
                    filter.CalculateGroupsAndSeries();
                    filter.UpdateEntityReferenceStrings();
                });

            using (ITransaction trans = session.BeginTransaction())
            {
                _groupFilterRepo.BatchUpdate(session, grpFilters);
                trans.Commit();
            }

            _log.Info("Group Filters updated");
        }

        /// <summary>
        /// Creates a single <see cref="SVR_AnimeGroup"/> for each <see cref="SVR_AnimeSeries"/> in <paramref name="seriesList"/>.
        /// </summary>
        /// <remarks>
        /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> to create groups for.</param>
        /// <returns>A sequence of the created <see cref="SVR_AnimeGroup"/>s.</returns>
        private IEnumerable<SVR_AnimeGroup> CreateGroupPerSeries(ISessionWrapper session,
            IReadOnlyList<SVR_AnimeSeries> seriesList)
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Auto-generating Groups with 1 group per series"};
            _log.Info("Generating AnimeGroups for {0} AnimeSeries", seriesList.Count);

            DateTime now = DateTime.Now;
            var newGroupsToSeries = new Tuple<SVR_AnimeGroup, SVR_AnimeSeries>[seriesList.Count];

            // Create one group per series
            for (int grp = 0; grp < seriesList.Count; grp++)
            {
                SVR_AnimeGroup group = new SVR_AnimeGroup();
                SVR_AnimeSeries series = seriesList[grp];

                group.Populate(series, now);
                newGroupsToSeries[grp] = new Tuple<SVR_AnimeGroup, SVR_AnimeSeries>(group, series);
            }

            using (ITransaction trans = session.BeginTransaction())
            {
                _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection());
                trans.Commit();
            }

            // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
            // (The caller of this method will be responsible for saving the AnimeSeries)
            foreach (Tuple<SVR_AnimeGroup, SVR_AnimeSeries> groupAndSeries in newGroupsToSeries)
            {
                groupAndSeries.Item2.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
            }

            _log.Info("Generated {0} AnimeGroups", newGroupsToSeries.Length);

            return newGroupsToSeries.Select(gts => gts.Item1);
        }

        /// <summary>
        /// Creates <see cref="SVR_AnimeGroup"/> that contain <see cref="SVR_AnimeSeries"/> that appear to be related.
        /// </summary>
        /// <remarks>
        /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> to create groups for.</param>
        /// <returns>A sequence of the created <see cref="SVR_AnimeGroup"/>s.</returns>
        private IEnumerable<SVR_AnimeGroup> AutoCreateGroupsWithRelatedSeries(ISessionWrapper session,
            IReadOnlyCollection<SVR_AnimeSeries> seriesList)
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Auto-generating Groups based on Relation Trees"};
            _log.Info("Auto-generating AnimeGroups for {0} AnimeSeries based on aniDB relationships", seriesList.Count);

            DateTime now = DateTime.Now;
            var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings(session);

            _log.Info(
                "The following exclusions will be applied when generating the groups: " + grpCalculator.Exclusions);

            // Group all of the specified series into their respective groups (keyed by the groups main anime ID)
            var seriesByGroup = seriesList.ToLookup(s => grpCalculator.GetGroupAnimeId(s.AniDB_ID));
            var newGroupsToSeries =
                new List<Tuple<SVR_AnimeGroup, IReadOnlyCollection<SVR_AnimeSeries>>>(seriesList.Count);

            foreach (var groupAndSeries in seriesByGroup)
            {
                int mainAnimeId = groupAndSeries.Key;
                SVR_AnimeSeries mainSeries = groupAndSeries.FirstOrDefault(series => series.AniDB_ID == mainAnimeId);
                SVR_AnimeGroup animeGroup = CreateAnimeGroup(mainSeries, mainAnimeId, now);

                newGroupsToSeries.Add(
                    new Tuple<SVR_AnimeGroup, IReadOnlyCollection<SVR_AnimeSeries>>(animeGroup,
                        groupAndSeries.AsReadOnlyCollection()));
            }

            using (ITransaction trans = session.BeginTransaction())
            {
                _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection());
                trans.Commit();
            }

            // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
            // (The caller of this method will be responsible for saving the AnimeSeries)
            foreach (var groupAndSeries in newGroupsToSeries)
            {
                foreach (SVR_AnimeSeries series in groupAndSeries.Item2)
                {
                    series.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
                }
            }

            _log.Info("Generated {0} AnimeGroups", newGroupsToSeries.Count);

            return newGroupsToSeries.Select(gts => gts.Item1);
        }

        /// <summary>
        /// Creates an <see cref="SVR_AnimeGroup"/> instance.
        /// </summary>
        /// <remarks>
        /// This method only creates an <see cref="SVR_AnimeGroup"/> instance. It does NOT save it to the database.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="mainSeries">The <see cref="SVR_AnimeSeries"/> whose name will represent the group (Optional. Pass <c>null</c> if not available).</param>
        /// <param name="mainAnimeId">The ID of the anime whose name will represent the group if <paramref name="mainSeries"/> is <c>null</c>.</param>
        /// <param name="now">The current date/time.</param>
        /// <returns>The created <see cref="SVR_AnimeGroup"/>.</returns>
        private SVR_AnimeGroup CreateAnimeGroup(SVR_AnimeSeries mainSeries, int mainAnimeId,
            DateTime now)
        {
            SVR_AnimeGroup animeGroup = new SVR_AnimeGroup();
            string groupName;

            if (mainSeries != null)
            {
                animeGroup.Populate(mainSeries, now);
                groupName = animeGroup.GroupName;
            }
            else // The anime chosen as the group's main anime doesn't actually have a series
            {
                SVR_AniDB_Anime mainAnime = _aniDbAnimeRepo.GetByAnimeID(mainAnimeId);

                animeGroup.Populate(mainAnime, now);
                groupName = animeGroup.GroupName;
            }

            // If the title appears to end with a year suffix, then remove it
            groupName = _truncateYearRegex.Replace(groupName, string.Empty);
            animeGroup.GroupName = groupName;
            animeGroup.SortName = groupName;

            return animeGroup;
        }

        /// <summary>
        /// Gets or creates an <see cref="SVR_AnimeGroup"/> for the specified series.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="series">The series for which the group is to be created/retrieved (Must be initialised first).</param>
        /// <returns>The <see cref="SVR_AnimeGroup"/> to use for the specified series.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="series"/> is <c>null</c>.</exception>
        public SVR_AnimeGroup GetOrCreateSingleGroupForSeries(ISessionWrapper session, SVR_AnimeSeries series)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (series == null)
                throw new ArgumentNullException(nameof(series));

            SVR_AnimeGroup animeGroup;

            if (_autoGroupSeries)
            {
                var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings(session);
                IReadOnlyList<int> grpAnimeIds = grpCalculator.GetIdsOfAnimeInSameGroup(series.AniDB_ID);
                // Try to find an existing AnimeGroup to add the series to
                // We basically pick the first group that any of the related series belongs to already
                animeGroup = grpAnimeIds.Where(id => id != series.AniDB_ID)
                    .Select(id => RepoFactory.AnimeSeries.GetByAnimeID(id))
                    .Where(s => s != null)
                    .Select(s => RepoFactory.AnimeGroup.GetByID(s.AnimeGroupID))
                    .FirstOrDefault(s => s != null);

                int mainAnimeId = grpCalculator.GetGroupAnimeId(series.AniDB_ID);
                if (animeGroup == null)
                {
                    // No existing group was found, so create a new one
                    SVR_AnimeSeries mainSeries = _animeSeriesRepo.GetByAnimeID(mainAnimeId);

                    animeGroup = CreateAnimeGroup(mainSeries, mainAnimeId, DateTime.Now);
                    RepoFactory.AnimeGroup.Save(animeGroup, true, true);
                }
                // Update the auto-refreshed details if the main series changed.
                else if (!animeGroup.DefaultAnimeSeriesID.HasValue && animeGroup.IsManuallyNamed == 0 && mainAnimeId == series.AniDB_ID)
                {
                    animeGroup.GroupName = animeGroup.SortName = series.GetSeriesName();
                    animeGroup.Description = series.GetAnime().Description;
                    animeGroup.MainAniDBAnimeID = mainAnimeId;
                    RepoFactory.AnimeGroup.Save(animeGroup, true, true);
                }
            }
            else // We're not auto grouping (e.g. we're doing group per series)
            {
                animeGroup = new SVR_AnimeGroup();
                animeGroup.Populate(series, DateTime.Now);
                RepoFactory.AnimeGroup.Save(animeGroup, true, true);
            }

            return animeGroup;
        }

        /// <summary>
        /// Re-creates all AnimeGroups based on the existing AnimeSeries.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public void RecreateAllGroups(ISessionWrapper session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            bool cmdProcGeneralPaused = ShokoService.CmdProcessorGeneral.Paused;
            bool cmdProcHasherPaused = ShokoService.CmdProcessorHasher.Paused;
            bool cmdProcImagesPaused = ShokoService.CmdProcessorImages.Paused;

            try
            {
                // Pause queues
                ShokoService.CmdProcessorGeneral.Paused = true;
                ShokoService.CmdProcessorHasher.Paused = true;
                ShokoService.CmdProcessorImages.Paused = true;

                ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo {Blocked = true, Status = "Beginning re-creation of all groups"};
                _log.Info("Beginning re-creation of all groups");

                IReadOnlyList<SVR_AnimeSeries> animeSeries = RepoFactory.AnimeSeries.GetAll();
                IReadOnlyCollection<SVR_AnimeGroup> createdGroups = null;
                SVR_AnimeGroup tempGroup = null;

                using (ITransaction trans = session.BeginTransaction())
                {
                    tempGroup = CreateTempAnimeGroup(session);
                    ClearGroupsAndDependencies(session, tempGroup.AnimeGroupID);
                    trans.Commit();
                }

                if (_autoGroupSeries)
                {
                    createdGroups = AutoCreateGroupsWithRelatedSeries(session, animeSeries)
                        .AsReadOnlyCollection();
                }
                else // Standard group re-create
                {
                    createdGroups = CreateGroupPerSeries(session, animeSeries)
                        .AsReadOnlyCollection();
                }

                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateAnimeSeriesContractsAndSave(session, animeSeries);
                    session.Delete(tempGroup); // We should no longer need the temporary group we created earlier
                    trans.Commit();
                }

                // We need groups and series cached for updating of AnimeGroup contracts to work
                _animeGroupRepo.Populate(session, displayname: false);
                _animeSeriesRepo.Populate(session, displayname: false);

                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateAnimeGroupsAndTheirContracts(session, createdGroups);
                    trans.Commit();
                }

                // We need to update the AnimeGroups cache again now that the contracts have been saved
                // (Otherwise updating Group Filters won't get the correct results)
                _animeGroupRepo.Populate(session, displayname: false);
                _animeGroupUserRepo.Populate(session, displayname: false);
                _groupFilterRepo.Populate(session, displayname: false);

                UpdateGroupFilters(session);

                _log.Info("Successfuly completed re-creating all groups");
            }
            catch (Exception e)
            {
                _log.Error(e, "An error occurred while re-creating all groups");

                try
                {
                    // If an error occurs then chances are the caches are in an inconsistent state. So re-populate them
                    _animeSeriesRepo.Populate();
                    _animeGroupRepo.Populate();
                    _groupFilterRepo.Populate();
                    _animeGroupUserRepo.Populate();
                }
                catch (Exception ie)
                {
                    _log.Warn(ie, "Failed to re-populate caches");
                }

                throw;
            }
            finally
            {
                ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo();
                // Un-pause queues (if they were previously running)
                ShokoService.CmdProcessorGeneral.Paused = cmdProcGeneralPaused;
                ShokoService.CmdProcessorHasher.Paused = cmdProcHasherPaused;
                ShokoService.CmdProcessorImages.Paused = cmdProcImagesPaused;
            }
        }

        public void RecreateAllGroups()
        {
            using (IStatelessSession session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                RecreateAllGroups(session.Wrap());
            }
        }

        public void RecalculateStatsContractsForGroup(SVR_AnimeGroup group)
        {
            using (ISession sessionNotWrapped = DatabaseFactory.SessionFactory.OpenSession())
            {
                var groups = new List<SVR_AnimeGroup> {group};
                var session = sessionNotWrapped.Wrap();
                var series = group.GetAllSeries(true);
                // recalculate series
                _log.Info($"Recalculating Series Stats and Contracts for Group: {group.GroupName} ({group.AnimeGroupID})");
                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateAnimeSeriesContractsAndSave(session, series);
                    trans.Commit();
                }

                // Update Cache so that group can recalculate
                series.ForEach(a => _animeSeriesRepo.Cache.Update(a));

                // Recalculate group
                _log.Info($"Recalculating Group Stats and Contracts for Group: {group.GroupName} ({group.AnimeGroupID})");
                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateAnimeGroupsAndTheirContracts(session, groups);
                    trans.Commit();
                }
                
                // update cache
                _animeGroupRepo.Cache.Update(group);
                var groupsUsers = _animeGroupUserRepo.GetByGroupID(group.AnimeGroupID);
                groupsUsers.ForEach(a => _animeGroupUserRepo.Cache.Update(a));

                // update filters
                _log.Info($"Recalculating Filters for Group: {group.GroupName} ({group.AnimeGroupID})");
                UpdateGroupFilters(session);

                _log.Info($"Done Recalculating Stats and Contracts for Group: {group.GroupName} ({group.AnimeGroupID})");
            }
        }
    }
}
