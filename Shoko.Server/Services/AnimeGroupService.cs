using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Services;

public class AnimeGroupService
{
    private readonly ILogger<AnimeGroupService> _logger;

    private readonly AnimeGroup_UserRepository _groupUsers;

    private readonly AnimeGroupRepository _groups;

    private readonly JMMUserRepository _jmmUsers;

    private readonly UserDataService _userDataService;

    public AnimeGroupService(ILogger<AnimeGroupService> logger, AnimeGroup_UserRepository groupUsers, AnimeGroupRepository groups, IUserDataService userDataService, JMMUserRepository jmmUsers)
    {
        _groupUsers = groupUsers;
        _logger = logger;
        _groups = groups;
        _userDataService = (UserDataService)userDataService;
        _jmmUsers = jmmUsers;
    }

    /// <summary>
    /// Update stats for all child groups and series
    /// This should only be called from the very top level group.
    /// </summary>
    public void UpdateStatsFromTopLevel(AnimeGroup? group, bool watchedStats, bool missingEpsStats)
    {
        if (group is not { AnimeGroupParentID: null })
        {
            return;
        }

        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} from Top Level (recursively) - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

        // now recursively update stats for all the child groups
        // and update the stats for the groups
        foreach (var grp in group.AllChildren)
        {
            UpdateStats(grp, watchedStats, missingEpsStats);
        }

        UpdateStats(group, watchedStats, missingEpsStats);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} from Top Level (recursively) in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Update the stats for this group based on the child series
    /// Assumes that all the AnimeSeries have had their stats updated already
    /// </summary>
    private void UpdateStats(AnimeGroup group, bool watchedStats, bool missingEpsStats)
    {
        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");
        var seriesList = group.AllSeries;

        // Reset the name/description for the group if needed.
        var mainSeries = group.IsManuallyNamed == 0 || group.OverrideDescription == 0 ? group.MainSeries : null;
        if (mainSeries is not null)
        {
            if (group.IsManuallyNamed == 0)
                group.GroupName = mainSeries.Title;
            if (group.OverrideDescription == 0)
                group.Description = mainSeries.PreferredOverview?.Value ?? string.Empty;
        }

        if (missingEpsStats)
        {
            UpdateMissingEpisodeStats(group, seriesList);
        }

        if (watchedStats)
        {
            var allUsers = _jmmUsers.GetAll();

            UpdateWatchedStats(group, seriesList, allUsers, (userRecord, _, isUpdated) =>
            {
                // Now update the stats for the groups
                _logger.LogTrace("Updating stats for {0}", ToString());
                if (isUpdated)
                    _groupUsers.Save(userRecord);
            });
        }

        group.DateTimeUpdated = DateTime.Now;
        _groups.Save(group, false);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Batch updates watched/missing episode stats for the specified sequence of <see cref="AnimeGroup"/>s.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the AnimeSeries have had their stats updated already.
    /// </remarks>
    /// <param name="animeGroups">The sequence of <see cref="AnimeGroup"/>s whose missing episode stats are to be updated.</param>
    /// <param name="watchedStats"><c>true</c> to update watched stats; otherwise, <c>false</c>.</param>
    /// <param name="missingEpsStats"><c>true</c> to update missing episode stats; otherwise, <c>false</c>.</param>
    /// <param name="createdGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="AnimeGroup_User"/> records
    /// that were created when updating watched stats.</param>
    /// <param name="updatedGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="AnimeGroup_User"/> records
    /// that were modified when updating watched stats.</param>
    /// <exception cref="ArgumentNullException"><paramref name="animeGroups"/> is <c>null</c>.</exception>
    public void BatchUpdateStats(IEnumerable<AnimeGroup> animeGroups, bool watchedStats = true,
        bool missingEpsStats = true,
        ICollection<AnimeGroup_User>? createdGroupUsers = null,
        ICollection<AnimeGroup_User>? updatedGroupUsers = null)
    {
        ArgumentNullException.ThrowIfNull(animeGroups);
        if (!watchedStats && !missingEpsStats)
            return; // Nothing to do

        var allUsers = _jmmUsers.GetAll();
        foreach (var animeGroup in animeGroups)
        {
            var animeSeries = animeGroup.AllSeries;
            if (missingEpsStats)
                UpdateMissingEpisodeStats(animeGroup, animeSeries);
            if (watchedStats)
            {
                UpdateWatchedStats(animeGroup, animeSeries, allUsers, (userRecord, isNew, isUpdated) =>
                {
                    if (isNew)
                    {
                        createdGroupUsers?.Add(userRecord);
                    }
                    else if (isUpdated)
                    {
                        updatedGroupUsers?.Add(userRecord);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Updates the watched stats for the specified anime group.
    /// </summary>
    /// <param name="animeGroup">The <see cref="AnimeGroup"/> that is to have it's watched stats updated.</param>
    /// <param name="seriesList">The list of <see cref="AnimeSeries"/> that belong to <paramref name="animeGroup"/>.</param>
    /// <param name="allUsers">A sequence of all users.</param>
    /// <param name="newAnimeGroupUsers">A method that will be called for each processed <see cref="AnimeGroup_User"/>
    /// and whether or not the <see cref="AnimeGroup_User"/> is new.</param>
    private void UpdateWatchedStats(AnimeGroup animeGroup,
        IReadOnlyList<AnimeSeries> seriesList,
        IEnumerable<JMMUser> allUsers, Action<AnimeGroup_User, bool, bool> newAnimeGroupUsers)
    {
        foreach (var user in allUsers)
        {
            _userDataService.UpdateWatchedStats(animeGroup, user, seriesList, newAnimeGroupUsers);
        }
    }

    /// <summary>
    /// Updates the missing episode stats for the specified anime group.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the AnimeSeries have had their stats updated already.
    /// </remarks>
    /// <param name="animeGroup">The <see cref="AnimeGroup"/> that is to have it's missing episode stats updated.</param>
    /// <param name="seriesList">The list of <see cref="AnimeSeries"/> that belong to <paramref name="animeGroup"/>.</param>
    private static void UpdateMissingEpisodeStats(AnimeGroup animeGroup,
        IEnumerable<AnimeSeries> seriesList)
    {
        var missingEpisodeCount = 0;
        var missingEpisodeCountGroups = 0;
        DateTime? latestEpisodeAirDate = null;

        seriesList.AsParallel().ForAll(series =>
        {
            Interlocked.Add(ref missingEpisodeCount, series.MissingEpisodeCount);
            Interlocked.Add(ref missingEpisodeCountGroups, series.MissingEpisodeCountGroups);

            // Now series.LatestEpisodeAirDate should never be greater than today
            if (!series.LatestEpisodeAirDate.HasValue)
            {
                return;
            }

            if (latestEpisodeAirDate == null)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
            else if (series.LatestEpisodeAirDate.Value > latestEpisodeAirDate.Value)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
        });

        animeGroup.MissingEpisodeCount = missingEpisodeCount;
        animeGroup.MissingEpisodeCountGroups = missingEpisodeCountGroups;
        animeGroup.LatestEpisodeAirDate = latestEpisodeAirDate;
    }
}
