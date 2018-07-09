using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;

namespace Shoko.Server.Tasks
{
    internal class AnimeGroupCreator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private const int DefaultBatchSize = 50;
        public const string TempGroupName = "AAA Migrating Groups AAA";
        private static readonly Regex _truncateYearRegex = new Regex(@"\s*\(\d{4}\)$");
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
            : this(ServerSettings.AutoGroupSeries)
        {
        }

        /// <summary>
        /// Creates a new group that series will be put in during group re-calculation.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>The temporary <see cref="SVR_AnimeGroup"/>.</returns>
        private SVR_AnimeGroup CreateTempAnimeGroup()
        {
            DateTime now = DateTime.Now;
            SVR_AnimeGroup tempGroup;
            using (var upd = Repo.AnimeGroup.BeginAddOrUpdate(() => Repo.AnimeGroup.GetByID(0)))
            {
                upd.Entity.GroupName = TempGroupName;
                upd.Entity.Description = TempGroupName;
                upd.Entity.SortName = TempGroupName;
                upd.Entity.DateTimeUpdated = now;
                upd.Entity.DateTimeCreated = now;
                tempGroup = upd.Commit();
            }

            // We won't use AnimeGroupRepository.Save because we don't need to perform all the extra stuff since this is for temporary use only
            //ssion.Insert(tempGroup);
            lock (Repo.AnimeGroup.Cache)
            {
                Repo.AnimeGroup.Cache.Update(tempGroup);
            }

            return tempGroup;
        }



        private void UpdateAnimeSeriesContractsAndSave(IReadOnlyCollection<SVR_AnimeSeries> series)
        {
            _log.Info("Updating contracts for AnimeSeries");

            // Update batches of AnimeSeries contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)

            Parallel.ForEach(series.Batch(DefaultBatchSize), new ParallelOptions {MaxDegreeOfParallelism = 4},
                body: (seriesBatch, state) =>
            {
                SVR_AnimeSeries.BatchUpdateContracts(seriesBatch);
                
            });               
            _log.Info("AnimeSeries contracts have been updated");
        }

        private void UpdateAnimeGroupsAndTheirContracts(IEnumerable<SVR_AnimeGroup> groups)
        {
            _log.Info("Updating statistics and contracts for AnimeGroups");

            var allCreatedGroupUsers = new ConcurrentBag<List<SVR_AnimeGroup_User>>();

            // Update batches of AnimeGroup contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
            // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
            Parallel.ForEach(groups.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
                body: (groupBatch, state, localSession) =>
                {
                    var createdGroupUsers = new List<SVR_AnimeGroup_User>(groupBatch.Length);

                    // We shouldn't need to keep track of updates to AnimeGroup_Users in the below call, because they should have all been deleted,
                    // therefore they should all be new
                    SVR_AnimeGroup.BatchUpdateStats(groupBatch, watchedStats: true, missingEpsStats: true,
                        createdGroupUsers: createdGroupUsers);
                    allCreatedGroupUsers.Add(createdGroupUsers);
                    SVR_AnimeGroup.BatchUpdateContracts(groupBatch, updateStats: true);
                });

            _log.Info("AnimeGroup statistics and contracts have been updated");

            _log.Info("Creating AnimeGroup_Users and updating plex/kodi contracts");


            // Insert the AnimeGroup_Users so that they get assigned a primary key before we update plex/kodi contracts
            // We need to repopulate caches for AnimeGroup_User and AnimeGroup because we've updated/inserted them
            // and they need to be up to date for the plex/kodi contract updating to work correctly

            // NOTE: There are situations in which UpdatePlexKodiContracts will cause database database writes to occur, so we can't
            // use Parallel.ForEach for the time being (If it was guaranteed to only read then we'd be ok)
            List<int> ids = allCreatedGroupUsers.SelectMany(a => a).Select(a => a.AnimeGroup_UserID).ToList();
            using (var upd = Repo.AnimeGroup_User.BeginBatchUpdate(()=>Repo.AnimeGroup_User.GetMany(ids)))
            {
                foreach (SVR_AnimeGroup_User guser in upd)
                {
                    guser.UpdatePlexKodiContracts_RA();
                    upd.Update(guser);
                }

                upd.Commit();
            }

            _log.Info("AnimeGroup_Users have been created");
        }

        /// <summary>
        /// Updates all Group Filters. This should be done as the last step.
        /// </summary>
        /// <remarks>
        /// Assumes that all caches are up to date.
        /// </remarks>
        private void UpdateGroupFilters()
        {
            _log.Info("Updating Group Filters");
            _log.Info("Calculating Tag Filters");
            Dictionary<int, ILookup<int, int>> seriesForTagGroupFilter = Repo.GroupFilter.CalculateAnimeSeriesPerTagGroupFilter();
            _log.Info("Caculating All Other Filters");
            IReadOnlyList<SVR_GroupFilter> grpFilters = Repo.GroupFilter.GetAll();
            IReadOnlyList<SVR_JMMUser> users = Repo.JMMUser.GetAll();

            // The main reason for doing this in parallel is because UpdateEntityReferenceStrings does JSON encoding
            // and is enough work that it can benefit from running in parallel
            var _toUpdate = grpFilters.Where(f => ((GroupFilterType)f.FilterType & GroupFilterType.Directory) !=
                                     GroupFilterType.Directory);
            Repo.GroupFilter.BatchAction(_toUpdate, _toUpdate.Count(), (filter, _) => 
            {
                filter.SeriesIds.Clear();

                if (filter.FilterType == (int)GroupFilterType.Tag)
                {
                    filter.SeriesIds[0] = seriesForTagGroupFilter[0][filter.GroupFilterID].ToHashSet();
                    filter.GroupsIds[0] = filter.SeriesIds[0]
                        .Select(id => Repo.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                        .Where(id => id != -1).ToHashSet();
                    foreach (var user in users)
                    {
                        filter.SeriesIds[user.JMMUserID] = seriesForTagGroupFilter[user.JMMUserID][filter.GroupFilterID].ToHashSet();
                        filter.GroupsIds[user.JMMUserID] = filter.SeriesIds[user.JMMUserID]
                            .Select(id => Repo.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                            .Where(id => id != -1).ToHashSet();
                    }
                }
                else // All other group filters are to be handled normally
                {
                    filter.CalculateGroupsAndSeries();
                }

                filter.UpdateEntityReferenceStrings();
            }, parallel: true);
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
        private IEnumerable<SVR_AnimeGroup> CreateGroupPerSeries(IReadOnlyList<SVR_AnimeSeries> seriesList)
        {
            _log.Info("Generating AnimeGroups for {0} AnimeSeries", seriesList.Count);

            DateTime now = DateTime.Now;
            List<SVR_AnimeGroup> grps=new List<SVR_AnimeGroup>();
            foreach (SVR_AnimeSeries s in seriesList)
            {
                SVR_AnimeGroup grp;
                using (var upd = Repo.AnimeGroup.BeginAddOrUpdate(() => null))
                {
                    upd.Entity.Populate_RA(s,now);
                    grp = upd.Commit((false, false, false));
                }
                using (var upd = Repo.AnimeSeries.BeginAddOrUpdate(() => Repo.AnimeSeries.GetByID(s.AnimeSeriesID)))
                {
                    upd.Entity.AnimeGroupID = grp.AnimeGroupID;
                    upd.Commit((false, false, true, false));
                }
                grps.Add(grp);
            }

            return grps;
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
        private IEnumerable<SVR_AnimeGroup> AutoCreateGroupsWithRelatedSeries(IReadOnlyCollection<SVR_AnimeSeries> seriesList)
        {
            _log.Info("Auto-generating AnimeGroups for {0} AnimeSeries based on aniDB relationships", seriesList.Count);

            DateTime now = DateTime.Now;
            var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();

            _log.Info(                "The following exclusions will be applied when generating the groups: " + grpCalculator.Exclusions);

            // Group all of the specified series into their respective groups (keyed by the groups main anime ID)



            var seriesByGroup = seriesList.ToLookup(s => grpCalculator.GetGroupAnimeId(s.AniDB_ID));
            List<SVR_AnimeGroup> grps = new List<SVR_AnimeGroup>();
            foreach (var groupAndSeries in seriesByGroup)
            {
                int mainAnimeId = groupAndSeries.Key;
                SVR_AnimeSeries mainSeries = groupAndSeries.FirstOrDefault(series => series.AniDB_ID == mainAnimeId);
                SVR_AnimeGroup grp;
                using (var upd = Repo.AnimeGroup.BeginAddOrUpdate(() => null))
                {
                    CreateAnimeGroup_RA(upd.Entity,mainSeries, mainAnimeId, now);
                    grp = upd.Commit((false, false, false));
                }
                using (var upd = Repo.AnimeSeries.BeginAddOrUpdate(() => Repo.AnimeSeries.GetByID(mainSeries.AnimeSeriesID)))
                {
                    upd.Entity.AnimeGroupID = grp.AnimeGroupID;
                    upd.Commit((false, false, true, false));
                }
                grps.Add(grp);
            }

            return grps;
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
        private void CreateAnimeGroup_RA(SVR_AnimeGroup animeGroup_ra, SVR_AnimeSeries mainSeries, int mainAnimeId,
            DateTime now)
        {
            SVR_AnimeGroup animeGroup = new SVR_AnimeGroup();
            string groupName;

            if (mainSeries != null)
            {
                animeGroup.Populate_RA(mainSeries, now);
                groupName = animeGroup.GroupName;
            }
            else // The anime chosen as the group's main anime doesn't actually have a series
            {
                SVR_AniDB_Anime mainAnime = Repo.AniDB_Anime.GetByID(mainAnimeId);

                animeGroup.Populate_RA(mainAnime, now);
                groupName = animeGroup.GroupName;
            }

            // If the title appears to end with a year suffix, then remove it
            groupName = _truncateYearRegex.Replace(groupName, string.Empty);
            animeGroup.GroupName = groupName;
            animeGroup.SortName = groupName;

            return;
        }

        /// <summary>
        /// Gets or creates an <see cref="SVR_AnimeGroup"/> for the specified series.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="series">The series for which the group is to be created/retrieved (Must be initialised first).</param>
        /// <returns>The <see cref="SVR_AnimeGroup"/> to use for the specified series.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="series"/> is <c>null</c>.</exception>
        public SVR_AnimeGroup GetOrCreateSingleGroupForSeries(SVR_AnimeSeries series)
        {
            if (series == null)
                throw new ArgumentNullException(nameof(series));

            SVR_AnimeGroup animeGroup;

            if (_autoGroupSeries)
            {
                var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
                IReadOnlyList<int> grpAnimeIds = grpCalculator.GetIdsOfAnimeInSameGroup(series.AniDB_ID);
                // Try to find an existing AnimeGroup to add the series to
                // We basically pick the first group that any of the related series belongs to already
                animeGroup = grpAnimeIds.Where(id => id != series.AniDB_ID)
                    .Select(id => Repo.AnimeSeries.GetByAnimeID(id))
                    .Where(s => s != null)
                    .Select(s => Repo.AnimeGroup.GetByID(s.AnimeGroupID))
                    .FirstOrDefault(s => s != null);

                if (animeGroup == null)
                {
                    // No existing group was found, so create a new one
                    int mainAnimeId = grpCalculator.GetGroupAnimeId(series.AniDB_ID);
                    SVR_AnimeSeries mainSeries = Repo.AnimeSeries.GetByAnimeID(mainAnimeId);

                    animeGroup = Repo.AnimeGroup.BeginAdd(CreateAnimeGroup(mainSeries, mainAnimeId, DateTime.Now)).Commit((true, true, true));
                }
            }
            else // We're not auto grouping (e.g. we're doing group per series)
            {
                using (var upd = Repo.AnimeGroup.BeginAdd())
                {
                    upd.Entity.Populate_RA(series, DateTime.Now);
                    animeGroup = upd.Commit((true, true, true));
                }
            }

            return animeGroup;
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
                animeGroup.Populate_RA(mainSeries, now);
                groupName = animeGroup.GroupName;
            }
            else // The anime chosen as the group's main anime doesn't actually have a series
            {
                SVR_AniDB_Anime mainAnime = Repo.AniDB_Anime.GetByAnimeID(mainAnimeId);

                animeGroup.Populate_RA(mainAnime, now);
                groupName = animeGroup.GroupName;
            }

            // If the title appears to end with a year suffix, then remove it
            groupName = _truncateYearRegex.Replace(groupName, string.Empty);
            animeGroup.GroupName = groupName;
            animeGroup.SortName = groupName;

            return animeGroup;
        }

        /// <summary>
        /// Re-creates all AnimeGroups based on the existing AnimeSeries.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public void RecreateAllGroups()
        {


            bool cmdProcGeneralPaused = ShokoService.CmdProcessorGeneral.Paused;
            bool cmdProcHasherPaused = ShokoService.CmdProcessorHasher.Paused;
            bool cmdProcImagesPaused = ShokoService.CmdProcessorImages.Paused;

            try
            {
                // Pause queues
                ShokoService.CmdProcessorGeneral.Paused = true;
                ShokoService.CmdProcessorHasher.Paused = true;
                ShokoService.CmdProcessorImages.Paused = true;

                _log.Info("Beginning re-creation of all groups");



                IReadOnlyList<SVR_AnimeSeries> animeSeries = Repo.AnimeSeries.GetAll();
                IEnumerable<SVR_AnimeGroup> createdGroups = null;
                SVR_AnimeGroup tempGroup = null;

                tempGroup = CreateTempAnimeGroup();
                _log.Info("Resetting AnimeSeries to an Empty Group");
                Repo.AnimeSeries.CleanAnimeGroups();
                _log.Info("Removing existing AnimeGroups and resetting GroupFilters");
                Repo.AnimeGroup_User.KillEmAll();
                Repo.AnimeGroup.KillEmAllExceptGrimorieOfZero();
                Repo.GroupFilter.CleanUpAllgroupsIds();
                _log.Info("AnimeGroups have been removed and GroupFilters have been reset");

                if (_autoGroupSeries)
                {
                    createdGroups = AutoCreateGroupsWithRelatedSeries(animeSeries);
                }
                else // Standard group re-create
                {
                    createdGroups = CreateGroupPerSeries(animeSeries);
                }
                UpdateAnimeSeriesContractsAndSave(animeSeries);
                Repo.AnimeGroup.Delete(0); // We should no longer need the temporary group we created earlier

                UpdateAnimeGroupsAndTheirContracts(createdGroups);
                UpdateGroupFilters();
                _log.Info("Successfuly completed re-creating all groups");
            }
            catch (Exception e)
            {
                _log.Error(e, "An error occurred while re-creating all groups");

                try
                {
                    // If an error occurs then chances are the caches are in an inconsistent state. So re-populate them
                }
                catch (Exception ie)
                {
                    _log.Warn(ie, "Failed to re-populate caches");
                }

                throw;
            }
            finally
            {
                // Un-pause queues (if they were previously running)
                ShokoService.CmdProcessorGeneral.Paused = cmdProcGeneralPaused;
                ShokoService.CmdProcessorHasher.Paused = cmdProcHasherPaused;
                ShokoService.CmdProcessorImages.Paused = cmdProcImagesPaused;
            }
        }


    }
}
