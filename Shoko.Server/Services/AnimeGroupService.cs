using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.v1.Models;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Services;

public class AnimeGroupService
{
    private readonly ILogger<AnimeGroupService> _logger;

    private readonly AnimeGroup_UserRepository _groupUsers;

    private readonly StoredReleaseInfoRepository _storedReleaseInfo;

    private readonly AnimeGroupRepository _groups;

    private readonly AnimeSeries_UserRepository _seriesUsers;

    private readonly UserDataService _userDataService;

    public AnimeGroupService(ILogger<AnimeGroupService> logger, AnimeGroup_UserRepository groupUsers, StoredReleaseInfoRepository storedReleaseInfo, AnimeGroupRepository groups, AnimeSeries_UserRepository seriesUsers, IUserDataService userDataService)
    {
        _groupUsers = groupUsers;
        _logger = logger;
        _storedReleaseInfo = storedReleaseInfo;
        _groups = groups;
        _seriesUsers = seriesUsers;
        _userDataService = (UserDataService)userDataService;
    }

    public void DeleteGroup(AnimeGroup group, bool updateParent = true)
    {
        // delete all sub groups
        foreach (var subGroup in group.AllChildren)
        {
            DeleteGroup(subGroup, false);
        }

        _groups.Delete(group);

        // finally update stats
        if (updateParent)
        {
            UpdateStatsFromTopLevel(group.Parent?.TopLevelAnimeGroup, true, true);
        }
    }

    public void SetMainSeries(AnimeGroup group, [CanBeNull] AnimeSeries series)
    {
        // Set the id before potentially resetting the fields, so the getter uses
        // the new id instead of the old.
        group.DefaultAnimeSeriesID = series?.AnimeSeriesID;

        ValidateMainSeries(group);

        // Reset the name/description if the group is not manually named.
        var current = series ?? (group.MainAniDBAnimeID.HasValue
            ? RepoFactory.AnimeSeries.GetByAnimeID(group.MainAniDBAnimeID.Value)
            : group.AllSeries.FirstOrDefault());
        if (group.IsManuallyNamed == 0 && current != null)
            group.GroupName = current!.Title;
        if (group.OverrideDescription == 0 && current != null)
            group.Description = current!.PreferredOverview?.Value ?? string.Empty;

        // Save the changes for this group only.
        group.DateTimeUpdated = DateTime.Now;
        _groups.Save(group, false);
    }

    public void ValidateMainSeries(AnimeGroup group)
    {
        if (group.MainAniDBAnimeID == null && group.DefaultAnimeSeriesID == null) return;
        var allSeries = group.AllSeries;

        // User overridden main series.
        if (group.DefaultAnimeSeriesID.HasValue && !allSeries.Any(series => series.AnimeSeriesID == group.DefaultAnimeSeriesID.Value))
        {
            throw new InvalidOperationException("Cannot set default series to a series that does not exist in the group");
        }

        // Auto selected main series.
        if (group.MainAniDBAnimeID.HasValue && !allSeries.Any(series => series.AniDB_ID == group.MainAniDBAnimeID.Value))
        {
            throw new InvalidOperationException("Cannot set default series to a series that does not exist in the group");
        }
    }

    [return: NotNullIfNotNull(nameof(group))]
    public CL_AnimeGroup_User? GetV1Contract(AnimeGroup? group, int userid)
    {
        if (group == null) return null;
        var groupSeries = group.AllSeries;
        var mainSeries = group.MainSeries ?? groupSeries.FirstOrDefault();
        var userDict = groupSeries
            .Select(a => _seriesUsers.GetByUserAndSeriesID(userid, a.AnimeSeriesID))
            .WhereNotNull()
            .ToDictionary(a => a.AnimeSeriesID);
        var votesByAnime = userDict.Values
            .Where(x => x.HasUserRating)
            .ToDictionary(a => a.AnimeSeriesID);
        var isFavorite = mainSeries is not null && userDict.TryGetValue(mainSeries.AnimeSeriesID, out var mainSeriesUser) && mainSeriesUser.IsFavorite;
        var contract = GetContract(group);
        var allVoteTotal = 0D;
        var permVoteTotal = 0D;
        var tempVoteTotal = 0D;
        var allVoteCount = 0;
        var permVoteCount = 0;
        var tempVoteCount = 0;
        foreach (var series in groupSeries)
        {
            if (votesByAnime.TryGetValue(series.AnimeSeriesID, out var seriesUserData))
            {
                allVoteCount++;
                allVoteTotal += seriesUserData.UserRating!.Value;

                switch (seriesUserData.UserRatingVoteType!.Value)
                {
                    case SeriesVoteType.Permanent:
                        permVoteCount++;
                        permVoteTotal += seriesUserData.UserRating!.Value;
                        break;
                    case SeriesVoteType.Temporary:
                        tempVoteCount++;
                        tempVoteTotal += seriesUserData.UserRating!.Value;
                        break;
                }
            }
        }
        var groupUserData = _groupUsers.GetByUserAndGroupID(userid, group.AnimeGroupID);
        if (groupUserData is not null)
        {
            contract.UnwatchedEpisodeCount = groupUserData.UnwatchedEpisodeCount;
            contract.WatchedEpisodeCount = groupUserData.WatchedEpisodeCount;
            contract.WatchedDate = groupUserData.WatchedDate;
            contract.PlayedCount = groupUserData.PlayedCount;
            contract.WatchedCount = groupUserData.WatchedCount;
            contract.StoppedCount = groupUserData.StoppedCount;
        }
        contract.IsFave = isFavorite ? 1 : 0;
        contract.Stat_UserVoteOverall = allVoteCount == 0 ? null : (decimal)Math.Round(allVoteTotal / allVoteCount / 100D, 2);
        contract.Stat_UserVotePermanent = permVoteCount == 0 ? null : (decimal)Math.Round(permVoteTotal / permVoteCount / 100D, 2);
        contract.Stat_UserVoteTemporary = tempVoteCount == 0 ? null : (decimal)Math.Round(tempVoteTotal / tempVoteCount / 100D, 2);

        return contract;
    }

    /// <summary>
    /// Rename all groups without a manually set name according to the current
    /// language preference.
    /// </summary>
    public void RenameAllGroups()
    {
        _logger.LogInformation("Starting RenameAllGroups");
        foreach (var grp in _groups.GetAll())
        {
            // Skip renaming any groups that are fully manually managed.
            if (grp.IsManuallyNamed == 1 && grp.OverrideDescription == 1)
                continue;

            var series = grp.MainSeries ?? grp.AllSeries.FirstOrDefault();
            if (series != null)
            {
                // Reset the name/description as needed.
                if (grp.IsManuallyNamed == 0)
                    grp.GroupName = series.Title;
                if (grp.OverrideDescription == 0)
                    grp.Description = series.PreferredOverview?.Value ?? string.Empty;

                // Save the changes for this group only.
                grp.DateTimeUpdated = DateTime.Now;
                _groups.Save(grp, false);
            }
        }
        _logger.LogInformation("Finished RenameAllGroups");
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
        var mainSeries = group.IsManuallyNamed == 0 || group.OverrideDescription == 0 ? group.MainSeries ?? seriesList.FirstOrDefault() : null;
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
            var allUsers = RepoFactory.JMMUser.GetAll();

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

        var allUsers = RepoFactory.JMMUser.GetAll();
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
    /// <param name="allUsers">A sequence of all JMM users.</param>
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

    [return: NotNullIfNotNull(nameof(animeGroup))]
    public CL_AnimeGroup_User? GetContract(AnimeGroup? animeGroup)
    {
        if (animeGroup == null) return null;
        var now = DateTime.Now;

        var contract = new CL_AnimeGroup_User
        {
            AnimeGroupID = animeGroup.AnimeGroupID,
            AnimeGroupParentID = animeGroup.AnimeGroupParentID,
            DefaultAnimeSeriesID = animeGroup.DefaultAnimeSeriesID,
            GroupName = animeGroup.GroupName,
            Description = animeGroup.Description,
            LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate,
            SortName = animeGroup.GroupName.ToSortName(),
            EpisodeAddedDate = animeGroup.EpisodeAddedDate,
            OverrideDescription = animeGroup.OverrideDescription,
            DateTimeUpdated = animeGroup.DateTimeUpdated,
            IsFave = 0,
            UnwatchedEpisodeCount = 0,
            WatchedEpisodeCount = 0,
            WatchedDate = null,
            PlayedCount = 0,
            WatchedCount = 0,
            StoppedCount = 0,
            MissingEpisodeCount = animeGroup.MissingEpisodeCount,
            MissingEpisodeCountGroups = animeGroup.MissingEpisodeCountGroups
        };

        var allSeriesForGroup = animeGroup.AllSeries;
        var allIDs = allSeriesForGroup.Select(a => a.AniDB_ID).ToArray();

        DateTime? airDateMin = null;
        DateTime? airDateMax = null;
        DateTime? groupEndDate = new DateTime(1980, 1, 1);
        DateTime? seriesCreatedDate = null;
        var isComplete = false;
        var hasFinishedAiring = false;
        var isCurrentlyAiring = false;
        var videoQualityEpisodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var allVidQualByGroup = allSeriesForGroup
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AniDB_ID))
            .Select(a => a.LegacySource)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        var tmdbShowXrefByAnime = allIDs
            .Select(RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID)
            .Where(a => a is { Count: > 0 })
            .ToDictionary(a => a[0].AnidbAnimeID);
        var tmdbMovieXrefByAnime = allIDs
            .Select(RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID)
            .Where(a => a is { Count: > 0 })
            .ToDictionary(a => a[0].AnidbAnimeID);
        var malXRefByAnime = allIDs.SelectMany(RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID).ToLookup(a => a.AnimeID);
        // Even though the contract value says 'has link', it's easier to think about whether it's missing
        var missingMALLink = false;
        var missingTMDBLink = false;
        var seriesCount = 0;
        var epCount = 0;

        var allYears = new HashSet<int>();
        var allSeasons = new SortedSet<string>(new CL_SeasonComparator());

        foreach (var series in allSeriesForGroup)
        {
            seriesCount++;

            var vidsTemp = RepoFactory.VideoLocal.GetByAniDBAnimeID(series.AniDB_ID);
            var crossRefs = RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID);
            var crossRefsLookup = crossRefs.ToLookup(cr => cr.EpisodeID);
            var dictVids = new Dictionary<string, VideoLocal>();

            foreach (var vid in vidsTemp)
            // Hashes may be repeated from multiple locations, but we don't care
            {
                dictVids[vid.Hash] = vid;
            }

            // All Video Quality Episodes
            // Try to determine if this anime has all the episodes available at a certain video quality
            // e.g.  the series has all episodes in blu-ray
            // Also look at languages
            var vidQualEpCounts = new Dictionary<string, int>();
            // video quality, count of episodes
            var anime = series.AniDB_Anime!;

            foreach (var ep in series.AllAnimeEpisodes)
            {
                if (ep.AniDB_Episode is null || ep.EpisodeType is not EpisodeType.Episode)
                {
                    continue;
                }

                var epVids = new List<VideoLocal>();

                foreach (var xref in crossRefsLookup[ep.AniDB_EpisodeID])
                {
                    if (xref.EpisodeID != ep.AniDB_EpisodeID)
                    {
                        continue;
                    }


                    if (dictVids.TryGetValue(xref.Hash, out var video))
                    {
                        epVids.Add(video);
                    }
                }

                var qualityAddedSoFar = new HashSet<string>();

                // Handle mutliple files of the same quality for one episode
                foreach (var vid in epVids)
                {
                    var release = vid.ReleaseInfo;

                    if (release == null)
                    {
                        continue;
                    }

                    if (!qualityAddedSoFar.Contains(release.LegacySource))
                    {
                        vidQualEpCounts.TryGetValue(release.LegacySource, out var srcCount);
                        vidQualEpCounts[release.LegacySource] =
                            srcCount +
                            1; // If the file source wasn't originally in the dictionary, then it will be set to 1

                        qualityAddedSoFar.Add(release.LegacySource);
                    }
                }
            }

            epCount += anime.EpisodeCountNormal;

            // Add all video qualities that span all of the normal episodes
            videoQualityEpisodes.UnionWith(
                vidQualEpCounts
                    .Where(vqec => anime.EpisodeCountNormal == vqec.Value)
                    .Select(vqec => vqec.Key));

            // Calculate Air Date
            var seriesAirDate = series.AirDate;

            if (seriesAirDate.HasValue)
            {
                if (airDateMin == null || seriesAirDate.Value < airDateMin.Value)
                {
                    airDateMin = seriesAirDate.Value;
                }

                if (airDateMax == null || seriesAirDate.Value > airDateMax.Value)
                {
                    airDateMax = seriesAirDate.Value;
                }
            }

            // Calculate end date
            // If the end date is NULL it actually means it is ongoing, so this is the max possible value
            var seriesEndDate = series.EndDate;

            if (seriesEndDate == null || groupEndDate == null)
            {
                groupEndDate = null;
            }
            else if (seriesEndDate.Value > groupEndDate.Value)
            {
                groupEndDate = seriesEndDate;
            }

            // Note - only one series has to be finished airing to qualify
            if (series.EndDate != null && series.EndDate.Value < now)
            {
                hasFinishedAiring = true;
            }

            // Note - only one series has to be finished airing to qualify
            if (series.EndDate == null || series.EndDate.Value > now)
            {
                isCurrentlyAiring = true;
            }

            // We evaluate IsComplete as true if
            // 1. series has finished airing
            // 2. user has all episodes locally
            // Note - only one series has to be complete for the group to be considered complete
            if (series.EndDate != null && series.EndDate.Value < now
                                       && series.MissingEpisodeCount == 0 &&
                                       series.MissingEpisodeCountGroups == 0)
            {
                isComplete = true;
            }

            // Calculate Series Created Date
            var createdDate = series.DateTimeCreated;

            if (seriesCreatedDate == null || createdDate < seriesCreatedDate.Value)
            {
                seriesCreatedDate = createdDate;
            }

            // For the group, if any of the series don't have a tmdb link
            // we will consider the group as not having a tmdb link
            var foundTMDBShowLink = tmdbShowXrefByAnime.TryGetValue(anime.AnimeID, out var _);
            var foundTMDBMovieLink = tmdbMovieXrefByAnime.TryGetValue(anime.AnimeID, out var _);
            var isMovie = anime.AnimeType is AnimeType.Movie;

            if (!foundTMDBShowLink && !foundTMDBMovieLink)
            {
                if (!series.IsTMDBAutoMatchingDisabled)
                {
                    missingTMDBLink = true;
                }
            }

            if (!malXRefByAnime[anime.AnimeID].Any())
            {
                missingMALLink = true;
            }

            var endYear = anime.EndYear;
            if (endYear == 0)
            {
                endYear = DateTime.Today.Year;
            }

            var startYear = anime.BeginYear;
            if (endYear < startYear)
            {
                endYear = startYear;
            }

            if (startYear != 0)
            {
                List<int> years;
                if (startYear == endYear)
                {
                    years = new List<int>
                    {
                        startYear
                    };
                }
                else
                {
                    years = Enumerable.Range(anime.BeginYear, endYear - anime.BeginYear + 1)
                        .Where(anime.IsInYear).ToList();
                }

                allYears.UnionWith(years);
                allSeasons.UnionWith(anime.YearlySeasons.Select(tuple => $"{tuple.Season} {tuple.Year}"));
            }
        }

        contract.Stat_AllYears = allYears;
        contract.Stat_AllSeasons = allSeasons;
        contract.Stat_AllTags = animeGroup.Tags.Select(a => a.TagName.Trim()).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllCustomTags = animeGroup.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllTitles = animeGroup.Titles.Select(a => a.Title).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AnimeTypes = allSeriesForGroup.Select(a => a.AniDB_Anime!.AnimeType.ToString().Replace('_', ' ')).WhereNotNull().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllVideoQuality = allVidQualByGroup;
        contract.Stat_IsComplete = isComplete;
        contract.Stat_HasFinishedAiring = hasFinishedAiring;
        contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
        contract.Stat_HasTvDBLink = false; // Deprecated
        contract.Stat_HasTraktLink = false; // Has a link if it isn't missing
        contract.Stat_HasMALLink = !missingMALLink; // Has a link if it isn't missing
        contract.Stat_HasMovieDBLink = !missingTMDBLink; // Has a link if it isn't missing
        contract.Stat_HasMovieDBOrTvDBLink = !missingTMDBLink; // Has a link if it isn't missing
        contract.Stat_SeriesCount = seriesCount;
        contract.Stat_EpisodeCount = epCount;
        contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
        contract.Stat_AirDate_Min = airDateMin;
        contract.Stat_AirDate_Max = airDateMax;
        contract.Stat_EndDate = groupEndDate;
        contract.Stat_SeriesCreatedDate = seriesCreatedDate;
        contract.Stat_AniDBRating = animeGroup.AniDBRating;
        contract.Stat_AudioLanguages = animeGroup.AllSeries
            .Select(a => a.AniDB_Anime)
            .WhereNotNull()
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AnimeID))
            .SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_SubtitleLanguages = animeGroup.AllSeries
            .Select(a => a.AniDB_Anime)
            .WhereNotNull()
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AnimeID))
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;

        return contract;
    }
}
