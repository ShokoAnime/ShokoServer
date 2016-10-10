using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JMMServer.Collections;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NLog;

namespace JMMServer.Tasks
{
    internal class RecreateAllGroupsTask
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private const int DefaultBatchSize = 50;
        private readonly AniDB_AnimeRepository _aniDbAnimeRepo = RepoFactory.AniDB_Anime;
        private readonly AnimeSeriesRepository _animeSeriesRepo = RepoFactory.AnimeSeries;
        private readonly AnimeGroupRepository _animeGroupRepo = RepoFactory.AnimeGroup;
        private readonly AnimeGroup_UserRepository _animeGroupUserRepo = RepoFactory.AnimeGroup_User;
        private readonly GroupFilterRepository _groupFilterRepo = RepoFactory.GroupFilter;
        private readonly JMMUserRepository _userRepo = RepoFactory.JMMUser;

        /// <summary>
        /// Creates a new group that series will be put in during group re-calculation.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>The temporary <see cref="AnimeGroup"/>.</returns>
        private AnimeGroup CreateTempAnimeGroup(ISessionWrapper session)
        {
            DateTime now = DateTime.Now;

            var tempGroup = new AnimeGroup
                {
                    GroupName = "AAA Migrating Groups AAA",
                    Description = "AAA Migrating Groups AAA",
                    SortName = "AAA Migrating Groups AAA",
                    DateTimeUpdated = now,
                    DateTimeCreated = now
                };

            // We won't use AnimeGroupRepository.Save because we don't need to perform all the extra stuff since this is for temporary use only
            session.Insert(tempGroup);

            return tempGroup;
        }

        /// <summary>
        /// Deletes the anime groups and user mappings as well as resetting group filters and moves all anime series into the specified group.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="tempGroupId">The ID of the temporary anime group to use for migration.</param>
        private void ClearGroupsAndDependencies(ISessionWrapper session, int tempGroupId)
        {
            _log.Info("Removing existing AnimeGroups and resetting GroupFilters");

            session.CreateSQLQuery(@"
                DELETE FROM AnimeGroup_User;
                DELETE FROM AnimeGroup WHERE AnimeGroupID <> :tempGroupId;

                UPDATE AnimeSeries SET AnimeGroupID = :tempGroupId;
                UPDATE GroupFilter SET GroupsIdsString = '{}';")
                .SetInt32("tempGroupId", tempGroupId)
                .ExecuteUpdate();

            // We've deleted/modified all AnimeGroup_User/AnimeGroup/AnimeSeries/GroupFilter records, so update caches to reflect that
            _animeGroupUserRepo.ClearCache();
            _animeGroupRepo.ClearCache();
            _animeSeriesRepo.ClearCache();
            _groupFilterRepo.ClearCache();
            _log.Info("AnimeGroups have been removed and GroupFilters have been reset");
        }

        private void UpdateAnimeSeriesContractsAndSave(ISessionWrapper session, IReadOnlyCollection<AnimeSeries> series)
        {
            _log.Info("Updating contracts for AnimeSeries");

            // Update batches of AnimeSeries contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(series.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
                localInit: () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
                body: (seriesBatch, state, localSession) =>
                    {
                        AnimeSeries.BatchUpdateContracts(localSession.Wrap(), seriesBatch);
                        return localSession;
                    },
                localFinally: localSession => { localSession.Dispose(); });

            _animeSeriesRepo.UpdateBatch(session, series);
            _log.Info("AnimeSeries contracts have been updated");
        }

        private void UpdateAnimeGroupsAndTheirContracts(ISessionWrapper session, IReadOnlyCollection<AnimeGroup> groups)
        {
            _log.Info("Updating statistics and contracts for AnimeGroups");

            var allCreatedGroupUsers = new ConcurrentBag<List<AnimeGroup_User>>();

            // Update batches of AnimeGroup contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(groups.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
                localInit: () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
                body: (groupBatch, state, localSession) =>
                    {
                        var createdGroupUsers = new List<AnimeGroup_User>(groupBatch.Length);

                        // We shouldn't need to keep track of updates to AnimeGroup_Users in the below call, because they should have all been deleted,
                        // therefore they should all be new
                        AnimeGroup.BatchUpdateStats(groupBatch, watchedStats: true, missingEpsStats: true, createdGroupUsers: createdGroupUsers);
                        allCreatedGroupUsers.Add(createdGroupUsers);
                        AnimeGroup.BatchUpdateContracts(localSession.Wrap(), groupBatch, updateStats: true);

                        return localSession;
                    },
                localFinally: localSession => { localSession.Dispose(); });

            _animeGroupRepo.UpdateBatch(session, groups);
            _log.Info("AnimeGroup statistics and contracts have been updated");

            _log.Info("Creating AnimeGroup_Users and updating plex/kodi contracts");

            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(allCreatedGroupUsers.SelectMany(groupUsers => groupUsers), groupUser =>
                {
                    groupUser.UpdatePlexKodiContracts();
                });

            _animeGroupUserRepo.InsertBatch(session, allCreatedGroupUsers.SelectMany(groupUsers => groupUsers));
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

            IReadOnlyList<GroupFilter> grpFilters = _groupFilterRepo.GetAll(session);
            ILookup<int, int> groupsForTagGroupFilter = _groupFilterRepo.CalculateAnimeGroupsPerTagGroupFilter(session);
            IReadOnlyList<JMMUser> users = _userRepo.GetAll();

            // The main reason for doing this in parallel is because UpdateEntityReferenceStrings does JSON encoding
            // and is enough work that it can benefit from running in parallel
            Parallel.ForEach(grpFilters.Where(f => ((GroupFilterType)f.FilterType & GroupFilterType.Directory) != GroupFilterType.Directory), filter =>
                {
                    var userGroupIds = filter.GroupsIds;

                    userGroupIds.Clear();

                    if (filter.FilterType == (int)GroupFilterType.Tag)
                    {
                        foreach (var user in users)
                        {
                            userGroupIds[user.JMMUserID] = groupsForTagGroupFilter[filter.GroupFilterID].ToHashSet();
                        }
                    }
                    else // All other group filters are to be handled normally
                    {
                        filter.EvaluateAnimeGroups();
                    }

                    filter.UpdateEntityReferenceStrings(updateGroups: true, updateSeries: false);
                });

            _groupFilterRepo.BatchUpdate(session, grpFilters);
            _log.Info("Group Filters updated");
        }

        /// <summary>
        /// Creates a single <see cref="AnimeGroup"/> for each <see cref="AnimeSeries"/> in <paramref name="seriesList"/>.
        /// </summary>
        /// <remarks>
        /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="seriesList">The list of <see cref="AnimeSeries"/> to create groups for.</param>
        /// <returns>A sequence of the created <see cref="AnimeGroup"/>s.</returns>
        private IEnumerable<AnimeGroup> CreateGroupPerSeries(ISessionWrapper session, IReadOnlyList<AnimeSeries> seriesList)
        {
            _log.Info("Generating AnimeGroups for {0} AnimeSeries", seriesList.Count);

            DateTime now = DateTime.Now;
            var newGroupsToSeries = new Tuple<AnimeGroup, AnimeSeries>[seriesList.Count];

            // Create one group per series
            for (int grp = 0; grp < seriesList.Count; grp++)
            {
                AnimeGroup group = new AnimeGroup();
                AnimeSeries series = seriesList[grp];

                group.Populate(series, now);
                newGroupsToSeries[grp] = new Tuple<AnimeGroup, AnimeSeries>(group, series);
            }

            using (ITransaction trans = session.BeginTransaction())
            {
                _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1));
                trans.Commit();
            }

            // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
            // (The caller of this method will be responsible for saving the AnimeSeries)
            foreach (Tuple<AnimeGroup, AnimeSeries> groupAndSeries in newGroupsToSeries)
            {
                groupAndSeries.Item2.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
            }

            _log.Info("Generated {0} AnimeGroups", newGroupsToSeries.Length);

            return newGroupsToSeries.Select(gts => gts.Item1);
        }

        /// <summary>
        /// Creates <see cref="AnimeGroup"/> that contain <see cref="AnimeSeries"/> that appear to be related.
        /// </summary>
        /// <remarks>
        /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="seriesList">The list of <see cref="AnimeSeries"/> to create groups for.</param>
        /// <returns>A sequence of the created <see cref="AnimeGroup"/>s.</returns>
        private IEnumerable<AnimeGroup> AutoCreateGroupsWithRelatedSeries(ISessionWrapper session, IReadOnlyCollection<AnimeSeries> seriesList)
        {
            _log.Info("Auto-generating AnimeGroups for {0} AnimeSeries based on aniDB relationships", seriesList.Count);

            DateTime now = DateTime.Now;
            Regex truncateYearRegex = new Regex(@"\s*\(\d{4}\)$");
            var grpCalculator = AutoAnimeGroupCalculator.Create(session);

            _log.Info("The following exclusions will be applied when generating the groups: " + grpCalculator.Exclusions);

            // Group all of the specified series into their respective groups (keyed by the groups main anime ID)
            var seriesByGroup = seriesList.ToLookup(s => grpCalculator.GetGroupAnimeId(s.AniDB_ID));
            var newGroupsToSeries = new List<Tuple<AnimeGroup, IReadOnlyCollection<AnimeSeries>>>(seriesList.Count);

            foreach (var groupAndSeries in seriesByGroup)
            {
                int mainAnimeId = groupAndSeries.Key;
                AnimeSeries mainSeries = groupAndSeries.FirstOrDefault(series => series.AniDB_ID == mainAnimeId);
                AnimeGroup animeGroup = new AnimeGroup();
                string groupName = null;

                if (mainSeries != null)
                {
                    animeGroup.Populate(mainSeries, now);
                    groupName = mainSeries.GetSeriesName(session);
                }
                else // The anime chosen as the group's main anime doesn't actually have a series
                {
                    AniDB_Anime mainAnime = _aniDbAnimeRepo.GetByAnimeID(mainAnimeId);

                    animeGroup.Populate(mainAnime, now);
                    groupName = mainAnime.GetFormattedTitle();
                }

                groupName = truncateYearRegex.Replace(groupName, String.Empty); // If the title appears to end with a year suffix, then remove it
                animeGroup.GroupName = groupName;
                animeGroup.SortName = groupName;

                newGroupsToSeries.Add(new Tuple<AnimeGroup, IReadOnlyCollection<AnimeSeries>>(animeGroup, groupAndSeries.AsReadOnlyCollection()));
            }

            using (ITransaction trans = session.BeginTransaction())
            {
                _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1));
                trans.Commit();
            }

            // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
            // (The caller of this method will be responsible for saving the AnimeSeries)
            foreach (var groupAndSeries in newGroupsToSeries)
            {
                foreach (AnimeSeries series in groupAndSeries.Item2)
                {
                    series.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
                }
            }

            _log.Info("Generated {0} AnimeGroups", newGroupsToSeries.Count);

            return newGroupsToSeries.Select(gts => gts.Item1);
        }

        public void Execute(ISessionWrapper session, bool autoGroupSeries = false)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            try
            {
                // Pause queues
                JMMService.CmdProcessorGeneral.Paused = true;
                JMMService.CmdProcessorHasher.Paused = true;
                JMMService.CmdProcessorImages.Paused = true;

                IReadOnlyList<AnimeSeries> animeSeries = RepoFactory.AnimeSeries.GetAll();
                IReadOnlyCollection<AnimeGroup> createdGroups = null;
                AnimeGroup tempGroup = null;

                using (ITransaction trans = session.BeginTransaction())
                {
                    tempGroup = CreateTempAnimeGroup(session);
                    ClearGroupsAndDependencies(session, tempGroup.AnimeGroupID);
                    trans.Commit();
                }

                if (autoGroupSeries)
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
                _animeGroupRepo.Populate(session);
                _animeSeriesRepo.Populate(session);

                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateAnimeGroupsAndTheirContracts(session, createdGroups);
                    trans.Commit();
                }

                // We need to update the AnimeGroups cache again now that the contracts have been saved
                // (Otherwise updating Group Filters won't get the correct results)
                _animeGroupRepo.Populate(session);
                _animeGroupUserRepo.Populate(session);
                _groupFilterRepo.Populate(session);

                using (ITransaction trans = session.BeginTransaction())
                {
                    UpdateGroupFilters(session);
                    trans.Commit();
                }
            }
            finally
            {
                // Un-pause queues
                JMMService.CmdProcessorGeneral.Paused = false;
                JMMService.CmdProcessorHasher.Paused = false;
                JMMService.CmdProcessorImages.Paused = false;
            }
        }

        public void Execute(bool autoGroupSeries = false)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                Execute(session.Wrap(), autoGroupSeries);
            }
        }
    }
}