using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Tasks;

public class AnimeGroupCreator
{
    private readonly ILogger<AnimeGroupCreator> _logger;
    private const int DefaultBatchSize = 50;
    public const string TempGroupName = "AAA Migrating Groups AAA";
    private static readonly Regex _truncateYearRegex = new(@"\s*\(\d{4}\)$");
    private readonly QueueHandler _queueHandler;
    private readonly AniDB_AnimeRepository _aniDbAnimeRepo = RepoFactory.AniDB_Anime;
    private readonly AnimeSeriesRepository _animeSeriesRepo = RepoFactory.AnimeSeries;
    private readonly AnimeGroupRepository _animeGroupRepo = RepoFactory.AnimeGroup;
    private readonly AnimeGroup_UserRepository _animeGroupUserRepo = RepoFactory.AnimeGroup_User;
    private readonly FilterPresetRepository _filterRepo = RepoFactory.FilterPreset;
    private readonly bool _autoGroupSeries;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeGroupCreator"/> class.
    /// </summary>
    /// <remarks>
    /// Uses the current server configuration to determine if auto grouping series is enabled.
    /// </remarks>
    public AnimeGroupCreator(ISettingsProvider settingsProvider, QueueHandler queueHandler, ILogger<AnimeGroupCreator> logger)
    {
        _queueHandler = queueHandler;
        _logger = logger;
        _autoGroupSeries = settingsProvider.GetSettings().AutoGroupSeries;
    }

    /// <summary>
    /// Creates a new group that series will be put in during group re-calculation.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <returns>The temporary <see cref="SVR_AnimeGroup"/>.</returns>
    private async Task<SVR_AnimeGroup> CreateTempAnimeGroup(ISessionWrapper session)
    {
        var now = DateTime.Now;

        var tempGroup = new SVR_AnimeGroup
        {
            GroupName = TempGroupName,
            Description = TempGroupName,
            DateTimeUpdated = now,
            DateTimeCreated = now
        };

        // We won't use AnimeGroupRepository.Save because we don't need to perform all the extra stuff since this is for temporary use only
        await session.InsertAsync(tempGroup);
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
    private async Task ClearGroupsAndDependencies(ISessionWrapper session, int tempGroupId)
    {
        ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo
        {
            Blocked = true, Status = "Removing existing AnimeGroups and resetting GroupFilters"
        };
        _logger.LogInformation("Removing existing AnimeGroups and resetting GroupFilters");

        await _animeGroupUserRepo.DeleteAll(session);
        await _animeGroupRepo.DeleteAll(session, tempGroupId);
        await BaseRepository.Lock(async () =>
        {
            await session.CreateSQLQuery(@"
                UPDATE AnimeSeries SET AnimeGroupID = :tempGroupId;")
                .SetInt32("tempGroupId", tempGroupId)
                .ExecuteUpdateAsync();
        });

        // We've deleted/modified all AnimeSeries/GroupFilter records, so update caches to reflect that
        _animeSeriesRepo.ClearCache();
        _logger.LogInformation("AnimeGroups have been removed and GroupFilters have been reset");
    }

    private async Task UpdateAnimeSeriesContractsAndSave(ISessionWrapper session,
        IReadOnlyCollection<SVR_AnimeSeries> series)
    {
        ServerState.Instance.DatabaseBlocked =
            new ServerState.DatabaseBlockedInfo { Blocked = true, Status = "Updating contracts for AnimeSeries" };
        _logger.LogInformation("Updating contracts for AnimeSeries");

        // Update batches of AnimeSeries contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
        // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
        Parallel.ForEach(series.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
            () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
            (seriesBatch, _, localSession) =>
            {
                SVR_AnimeSeries.BatchUpdateContracts(localSession.Wrap(), seriesBatch);
                return localSession;
            },
            localSession => { localSession.Dispose(); });

        await _animeSeriesRepo.UpdateBatch(session, series);
        _logger.LogInformation("AnimeSeries contracts have been updated");
    }

    private async Task UpdateAnimeGroupsAndTheirContracts(IReadOnlyCollection<SVR_AnimeGroup> groups)
    {
        ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo
        {
            Blocked = true, Status = "Updating statistics and contracts for AnimeGroups"
        };
        _logger.LogInformation("Updating statistics and contracts for AnimeGroups");

        var allCreatedGroupUsers = new ConcurrentBag<List<SVR_AnimeGroup_User>>();

        // Update batches of AnimeGroup contracts in parallel. Each parallel branch requires it's own session since NHibernate sessions aren't thread safe.
        // The reason we're doing this in parallel is because updating contacts does a reasonable amount of work (including LZ4 compression)
        Parallel.ForEach(groups.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
            () => DatabaseFactory.SessionFactory.OpenStatelessSession(),
            (groupBatch, _, localSession) =>
            {
                var createdGroupUsers = new List<SVR_AnimeGroup_User>(groupBatch.Length);

                // We shouldn't need to keep track of updates to AnimeGroup_Users in the below call, because they should have all been deleted,
                // therefore they should all be new
                SVR_AnimeGroup.BatchUpdateStats(groupBatch, true, true,
                    createdGroupUsers);
                allCreatedGroupUsers.Add(createdGroupUsers);
                SVR_AnimeGroup.BatchUpdateContracts(localSession.Wrap(), groupBatch, true);

                return localSession;
            },
            localSession => { localSession.Dispose(); });

        using var session = DatabaseFactory.SessionFactory.OpenSession().Wrap();
        await BaseRepository.Lock(session, groups, async (s, g) => await _animeGroupRepo.UpdateBatch(s, g));
        _logger.LogInformation("AnimeGroup statistics and contracts have been updated");

        ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo
        {
            Blocked = true, Status = "Creating AnimeGroup_Users and updating plex/kodi contracts"
        };
        _logger.LogInformation("Creating AnimeGroup_Users and updating plex/kodi contracts");

        var animeGroupUsers = allCreatedGroupUsers.SelectMany(groupUsers => groupUsers)
            .ToList();

        // Insert the AnimeGroup_Users so that they get assigned a primary key before we update plex/kodi contracts
        await BaseRepository.Lock(session, animeGroupUsers, async (s, u) => await _animeGroupUserRepo.InsertBatch(s, u));
        // We need to repopulate caches for AnimeGroup_User and AnimeGroup because we've updated/inserted them
        // and they need to be up to date for the plex/kodi contract updating to work correctly
        _animeGroupUserRepo.Populate(session, false);
        _animeGroupRepo.Populate(session, false);

        await BaseRepository.Lock(session, animeGroupUsers, async (s, u) => await _animeGroupUserRepo.UpdateBatch(s, u));
        _logger.LogInformation("AnimeGroup_Users have been created");
    }

    /// <summary>
    /// Updates all Group Filters. This should be done as the last step.
    /// </summary>
    /// <remarks>
    /// Assumes that all caches are up to date.
    /// </remarks>
    private void UpdateGroupFilters()
    {
        _logger.LogInformation("Updating Group Filters");
        _logger.LogInformation("Calculating Tag Filters");
        ServerState.Instance.DatabaseBlocked =
            new ServerState.DatabaseBlockedInfo { Blocked = true, Status = "Calculating Tag Filters" };
        _filterRepo.CreateOrVerifyDirectoryFilters();

        _logger.LogInformation("Group Filters updated");
    }

    /// <summary>
    /// Creates a single <see cref="SVR_AnimeGroup"/> for each <see cref="SVR_AnimeSeries"/> in <paramref name="seriesList"/>.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> to create groups for.</param>
    /// <returns>A sequence of the created <see cref="SVR_AnimeGroup"/>s.</returns>
    private async Task<IEnumerable<SVR_AnimeGroup>> CreateGroupPerSeries(ISessionWrapper session, IReadOnlyList<SVR_AnimeSeries> seriesList)
    {
        ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo
        {
            Blocked = true, Status = "Auto-generating Groups with 1 group per series"
        };
        _logger.LogInformation("Generating AnimeGroups for {Count} AnimeSeries", seriesList.Count);

        var now = DateTime.Now;
        var newGroupsToSeries = new Tuple<SVR_AnimeGroup, SVR_AnimeSeries>[seriesList.Count];

        // Create one group per series
        for (var grp = 0; grp < seriesList.Count; grp++)
        {
            var group = new SVR_AnimeGroup();
            var series = seriesList[grp];

            group.Populate(series, now);
            newGroupsToSeries[grp] = new Tuple<SVR_AnimeGroup, SVR_AnimeSeries>(group, series);
        }

        await BaseRepository.Lock(async () => await _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection()));

        // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
        // (The caller of this method will be responsible for saving the AnimeSeries)
        foreach (var groupAndSeries in newGroupsToSeries)
        {
            groupAndSeries.Item2.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
        }

        _logger.LogInformation("Generated {Count} AnimeGroups", newGroupsToSeries.Length);

        return newGroupsToSeries.Select(gts => gts.Item1);
    }

    /// <summary>
    /// Creates <see cref="SVR_AnimeGroup"/> that contain <see cref="SVR_AnimeSeries"/> that appear to be related.
    /// </summary>
    /// <remarks>
    /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
    /// </remarks>
    /// <param name="session"></param>
    /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> to create groups for.</param>
    /// <returns>A sequence of the created <see cref="SVR_AnimeGroup"/>s.</returns>
    private async Task<IEnumerable<SVR_AnimeGroup>> AutoCreateGroupsWithRelatedSeries(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeSeries> seriesList)
    {
        ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo
        {
            Blocked = true, Status = "Auto-generating Groups based on Relation Trees"
        };
        _logger.LogInformation("Auto-generating AnimeGroups for {Count} AnimeSeries based on aniDB relationships", seriesList.Count);

        var now = DateTime.Now;
        var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();

        _logger.LogInformation("The following exclusions will be applied when generating the groups: {Exclusions}", grpCalculator.Exclusions);

        // Group all of the specified series into their respective groups (keyed by the groups main anime ID)
        var seriesByGroup = seriesList.ToLookup(s => grpCalculator.GetGroupAnimeId(s.AniDB_ID));
        var newGroupsToSeries =
            new List<Tuple<SVR_AnimeGroup, IReadOnlyCollection<SVR_AnimeSeries>>>(seriesList.Count);

        foreach (var groupAndSeries in seriesByGroup)
        {
            var mainAnimeId = groupAndSeries.Key;
            var mainSeries = groupAndSeries.FirstOrDefault(series => series.AniDB_ID == mainAnimeId);
            var animeGroup = CreateAnimeGroup(mainSeries, mainAnimeId, now);

            newGroupsToSeries.Add(
                new Tuple<SVR_AnimeGroup, IReadOnlyCollection<SVR_AnimeSeries>>(animeGroup,
                    groupAndSeries.AsReadOnlyCollection()));
        }

        await BaseRepository.Lock(async () => await _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection()));

        // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
        // (The caller of this method will be responsible for saving the AnimeSeries)
        foreach (var groupAndSeries in newGroupsToSeries)
        {
            foreach (var series in groupAndSeries.Item2)
            {
                series.AnimeGroupID = groupAndSeries.Item1.AnimeGroupID;
            }
        }

        _logger.LogInformation("Generated {Count} AnimeGroups", newGroupsToSeries.Count);

        return newGroupsToSeries.Select(gts => gts.Item1);
    }

    /// <summary>
    /// Creates an <see cref="SVR_AnimeGroup"/> instance.
    /// </summary>
    /// <remarks>
    /// This method only creates an <see cref="SVR_AnimeGroup"/> instance. It does NOT save it to the database.
    /// </remarks>
    /// <param name="mainSeries">The <see cref="SVR_AnimeSeries"/> whose name will represent the group (Optional. Pass <c>null</c> if not available).</param>
    /// <param name="mainAnimeId">The ID of the anime whose name will represent the group if <paramref name="mainSeries"/> is <c>null</c>.</param>
    /// <param name="now">The current date/time.</param>
    /// <returns>The created <see cref="SVR_AnimeGroup"/>.</returns>
    private SVR_AnimeGroup CreateAnimeGroup(SVR_AnimeSeries mainSeries, int mainAnimeId,
        DateTime now)
    {
        var animeGroup = new SVR_AnimeGroup();
        string groupName;

        if (mainSeries != null)
        {
            animeGroup.Populate(mainSeries, now);
            groupName = animeGroup.GroupName;
        }
        else // The anime chosen as the group's main anime doesn't actually have a series
        {
            var mainAnime = _aniDbAnimeRepo.GetByAnimeID(mainAnimeId);

            animeGroup.Populate(mainAnime, now);
            groupName = animeGroup.GroupName;
        }

        // If the title appears to end with a year suffix, then remove it
        groupName = _truncateYearRegex.Replace(groupName, string.Empty);
        animeGroup.GroupName = groupName;

        return animeGroup;
    }

    /// <summary>
    /// Gets or creates an <see cref="SVR_AnimeGroup"/> for the specified series.
    /// </summary>
    /// <param name="series">The series for which the group is to be created/retrieved (Must be initialised first).</param>
    /// <returns>The <see cref="SVR_AnimeGroup"/> to use for the specified series.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    public SVR_AnimeGroup GetOrCreateSingleGroupForSeries(SVR_AnimeSeries series)
    {
        if (series == null)
        {
            throw new ArgumentNullException(nameof(series));
        }

        SVR_AnimeGroup animeGroup;

        if (_autoGroupSeries)
        {
            var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
            var grpAnimeIds = grpCalculator.GetIdsOfAnimeInSameGroup(series.AniDB_ID);
            // Try to find an existing AnimeGroup to add the series to
            // We basically pick the first group that any of the related series belongs to already
            animeGroup = grpAnimeIds.Where(id => id != series.AniDB_ID)
                .Select(id => RepoFactory.AnimeSeries.GetByAnimeID(id))
                .Where(s => s != null)
                .Select(s => RepoFactory.AnimeGroup.GetByID(s.AnimeGroupID))
                .FirstOrDefault(s => s != null);

            var mainAnimeId = grpCalculator.GetGroupAnimeId(series.AniDB_ID);
            // No existing group was found, so create a new one.
            if (animeGroup == null)
            {
                // Find the main series for the group.
                var mainSeries = series.AniDB_ID == mainAnimeId ?
                    series :
                    _animeSeriesRepo.GetByAnimeID(mainAnimeId);
                animeGroup = CreateAnimeGroup(mainSeries, mainAnimeId, DateTime.Now);
                RepoFactory.AnimeGroup.Save(animeGroup, true, true);
            }
            // Update the group details if we have the main series for the group.
            else if (mainAnimeId == series.AniDB_ID)
            {
                // Always update the automatic main id.
                animeGroup.MainAniDBAnimeID = mainAnimeId;
                // Update the auto-refreshed details if the main series changed
                // and no default series is set.
                if (!animeGroup.DefaultAnimeSeriesID.HasValue)
                {
                    // Override the group name if the group is not manually named.
                    if (animeGroup.IsManuallyNamed == 0)
                    {
                        animeGroup.GroupName = series.GetSeriesName();
                    }
                    // Override the group desc. if the group doesn't have an override.
                    if (animeGroup.OverrideDescription == 0)
                        animeGroup.Description = series.GetAnime().Description;
                }
                animeGroup.DateTimeUpdated = DateTime.Now;
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
    private async Task RecreateAllGroups(ISessionWrapper session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var paused = _queueHandler.Paused;

        try
        {
            // Pause queues
            if (!paused) await _queueHandler.Pause();

            ServerState.Instance.DatabaseBlocked =
                new ServerState.DatabaseBlockedInfo { Blocked = true, Status = "Beginning re-creation of all groups" };
            _logger.LogInformation("Beginning re-creation of all groups");

            var animeSeries = RepoFactory.AnimeSeries.GetAll();
            SVR_AnimeGroup tempGroup = null;

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                tempGroup = await CreateTempAnimeGroup(session);
                await ClearGroupsAndDependencies(session, tempGroup.AnimeGroupID);
                await trans.CommitAsync();
            });

            var createdGroups = _autoGroupSeries
                ? (await AutoCreateGroupsWithRelatedSeries(session, animeSeries)).AsReadOnlyCollection()
                : (await CreateGroupPerSeries(session, animeSeries)).AsReadOnlyCollection();

            await UpdateAnimeSeriesContractsAndSave(session, animeSeries);

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                await session.DeleteAsync(tempGroup); // We should no longer need the temporary group we created earlier
                await trans.CommitAsync();
            });

            // We need groups and series cached for updating of AnimeGroup contracts to work
            _animeGroupRepo.Populate(session, false);
            _animeSeriesRepo.Populate(session, false);
            
            await UpdateAnimeGroupsAndTheirContracts(createdGroups);

            // We need to update the AnimeGroups cache again now that the contracts have been saved
            // (Otherwise updating Group Filters won't get the correct results)
            _animeGroupRepo.Populate(session, false);
            _animeGroupUserRepo.Populate(session, false);

            UpdateGroupFilters();

            _logger.LogInformation("Successfully completed re-creating all groups");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while re-creating all groups");

            try
            {
                // If an error occurs then chances are the caches are in an inconsistent state. So re-populate them
                _animeSeriesRepo.Populate();
                _animeGroupRepo.Populate();
                _animeGroupUserRepo.Populate();
            }
            catch (Exception ie)
            {
                _logger.LogWarning(ie, "Failed to re-populate caches");
            }

            throw;
        }
        finally
        {
            ServerState.Instance.DatabaseBlocked = new ServerState.DatabaseBlockedInfo();
            // Un-pause queues (if they were previously running)
            if (!paused) await _queueHandler.Resume();
        }
    }

    public async Task RecreateAllGroups()
    {
        using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
        await RecreateAllGroups(session.Wrap());
    }

    public async Task RecalculateStatsContractsForGroup(SVR_AnimeGroup group)
    {
        using var sessionNotWrapped = DatabaseFactory.SessionFactory.OpenSession();
        var groups = new List<SVR_AnimeGroup> { group };
        var session = sessionNotWrapped.Wrap();
        var series = group.GetAllSeries(true);
        // recalculate series
        _logger.LogInformation("Recalculating Series Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.AnimeGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeSeriesContractsAndSave(session, series);
            await trans.CommitAsync();
        });

        // Update Cache so that group can recalculate
        series.ForEach(a => _animeSeriesRepo.Cache.Update(a));

        // Recalculate group
        _logger.LogInformation("Recalculating Group Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.AnimeGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeGroupsAndTheirContracts(groups);
            await trans.CommitAsync();
        });

        // update cache
        _animeGroupRepo.Cache.Update(group);
        var groupsUsers = _animeGroupUserRepo.GetByGroupID(group.AnimeGroupID);
        groupsUsers.ForEach(a => _animeGroupUserRepo.Cache.Update(a));

        // update filters
        _logger.LogInformation("Recalculating Filters for Group: {Name} ({ID})", group.GroupName, group.AnimeGroupID);
        UpdateGroupFilters();

        _logger.LogInformation("Done Recalculating Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.AnimeGroupID);
    }
}
