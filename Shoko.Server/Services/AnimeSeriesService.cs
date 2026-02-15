using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Utilities;

using AnimeType = Shoko.Abstractions.Enums.AnimeType;
using EpisodeType = Shoko.Abstractions.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Services;

public class AnimeSeriesService
{
    private readonly ILogger<AnimeSeriesService> _logger;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly AnimeGroupService _groupService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly UserDataService _userDataService;

    public AnimeSeriesService(ILogger<AnimeSeriesService> logger, ISchedulerFactory schedulerFactory, AnimeGroupService groupService, VideoLocal_UserRepository vlUsers, IVideoReleaseService videoReleaseService, IUserDataService userDataService)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _groupService = groupService;
        _vlUsers = vlUsers;
        _videoReleaseService = videoReleaseService;
        _userDataService = (UserDataService)userDataService;
    }

    public async Task<(bool, Dictionary<AnimeEpisode, UpdateReason>)> CreateAnimeEpisodes(AnimeSeries series)
    {
        var anime = series.AniDB_Anime;
        if (anime == null)
            return (false, []);
        var anidbEpisodes = anime.AniDBEpisodes;
        // Cleanup deleted episodes
        var epsToRemove = RepoFactory.AnimeEpisode.GetBySeriesID(series.AnimeSeriesID)
            .Where(a => a.AniDB_Episode is null)
            .ToList();
        var filesToUpdate = epsToRemove
            .SelectMany(a => a.FileCrossReferences)
            .ToList();
        var vlIDsToUpdate = filesToUpdate
            .Select(a => a.VideoLocal)
            .WhereNotNull()
            .ToList();

        // remove the current release and schedule a recheck for the file if
        // auto match is enabled.
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var video in vlIDsToUpdate)
        {
            await _videoReleaseService.ClearReleaseForVideo(video);
            await _videoReleaseService.ScheduleFindReleaseForVideo(video);
        }

        _logger.LogTrace($"Generating {anidbEpisodes.Count} episodes for {anime.MainTitle}");

        var oneForth = (int)Math.Round(anidbEpisodes.Count / 4D, 0, MidpointRounding.AwayFromZero);
        var oneHalf = (int)Math.Round(anidbEpisodes.Count / 2D, 0, MidpointRounding.AwayFromZero);
        var threeFourths = (int)Math.Round(anidbEpisodes.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);
        var episodeDict = new Dictionary<AnimeEpisode, UpdateReason>();
        for (var i = 0; i < anidbEpisodes.Count; i++)
        {
            if (i == oneForth)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 25%");
            }

            if (i == oneHalf)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 50%");
            }

            if (i == threeFourths)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 75%");
            }

            if (i == anidbEpisodes.Count - 1)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 100%");
            }

            var anidbEpisode = anidbEpisodes[i];
            var (shokoEpisode, isNew, isUpdated) = CreateAnimeEpisode(anidbEpisode, series.AnimeSeriesID);
            if (isUpdated)
                episodeDict.Add(shokoEpisode, isNew ? UpdateReason.Added : UpdateReason.Updated);
        }

        RepoFactory.AnimeEpisode.Delete(epsToRemove);

        // Add removed episodes to the dictionary.
        foreach (var episode in epsToRemove)
            episodeDict.Add(episode, UpdateReason.Removed);

        return (
            episodeDict.ContainsValue(UpdateReason.Added) || epsToRemove.Count > 0,
            episodeDict
        );
    }

    private (AnimeEpisode episode, bool isNew, bool isUpdated) CreateAnimeEpisode(AniDB_Episode episode, int animeSeriesID)
    {
        // check if there is an existing episode for this EpisodeID
        var existingEp = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
        var isNew = existingEp is null;
        existingEp ??= new();

        var old = existingEp.DeepClone();
        existingEp.Populate(episode);
        existingEp.AnimeSeriesID = animeSeriesID;

        var updated = !old.Equals(existingEp);
        if (isNew || updated)
            RepoFactory.AnimeEpisode.Save(existingEp);

        _userDataService.CreateUserRecordsForNewEpisode(existingEp);

        return (existingEp, isNew, updated);
    }

    public void MoveSeries(AnimeSeries series, AnimeGroup newGroup, bool updateGroupStats = true, bool updateEvent = true)
    {
        // Skip moving series if it's already part of the group.
        if (series.AnimeGroupID == newGroup.AnimeGroupID)
            return;

        var oldGroupID = series.AnimeGroupID;
        // Update the stats for the series and group.
        series.AnimeGroupID = newGroup.AnimeGroupID;
        series.DateTimeUpdated = DateTime.Now;
        UpdateStats(series, true, true);
        if (updateGroupStats)
            _groupService.UpdateStatsFromTopLevel(newGroup.TopLevelAnimeGroup, true, true);

        var oldGroup = RepoFactory.AnimeGroup.GetByID(oldGroupID);
        if (oldGroup is not null)
        {
            // This was the only one series in the group so delete the now orphan group.
            if (oldGroup.AllSeries.Count == 0)
            {
                _groupService.DeleteGroup(oldGroup, false);
            }
            else
            {
                var updatedOldGroup = false;
                if (oldGroup.DefaultAnimeSeriesID.HasValue && oldGroup.DefaultAnimeSeriesID.Value == series.AnimeSeriesID)
                {
                    oldGroup.DefaultAnimeSeriesID = null;
                    updatedOldGroup = true;
                }

                if (oldGroup.MainAniDBAnimeID.HasValue && oldGroup.MainAniDBAnimeID.Value == series.AniDB_ID)
                {
                    oldGroup.MainAniDBAnimeID = null;
                    updatedOldGroup = true;
                }

                if (updatedOldGroup)
                    RepoFactory.AnimeGroup.Save(oldGroup);
            }

            // Update the top group
            var topGroup = oldGroup.TopLevelAnimeGroup;
            if (topGroup.AnimeGroupID != oldGroup.AnimeGroupID)
            {
                _groupService.UpdateStatsFromTopLevel(topGroup, true, true);
            }
        }

        if (updateEvent)
            ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
    }

    public async Task QueueUpdateStats(AnimeSeries series)
    {
        if (series == null) return;
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<RefreshAnimeStatsJob>(c => c.AnimeID = series.AniDB_ID);
    }

    public void UpdateStats(AnimeSeries? series, bool watchedStats, bool missingEpsStats)
    {
        if (series == null) return;
        lock (this)
        {
            var start = DateTime.Now;
            var initialStart = DateTime.Now;
            var name = series.AniDB_Anime?.MainTitle ?? series.AniDB_ID.ToString();
            _logger.LogInformation("Starting Updating STATS for SERIES {Name} - Watched Stats: {WatchedStats}, Missing Episodes: {MissingEpsStats}", name,
                watchedStats, missingEpsStats);

            var startEps = DateTime.Now;
            var eps = series.AllAnimeEpisodes;
            var tsEps = DateTime.Now - startEps;
            _logger.LogTrace("Got episodes for SERIES {Name} in {Elapsed}ms", name, tsEps.TotalMilliseconds);

            // Ensure the episode added date is accurate.
            series.EpisodeAddedDate = RepoFactory.StoredReleaseInfo.GetByAnidbAnimeID(series.AniDB_ID)
                .Select(a => a.LastUpdatedAt)
                .DefaultIfEmpty()
                .Max();

            if (watchedStats) UpdateWatchedStats(series, eps, name, ref start);
            if (missingEpsStats) UpdateMissingEpisodeStats(series, eps, name, ref start);

            // Skip group filters if we are doing group stats, as the group stats will regenerate group filters
            RepoFactory.AnimeSeries.Save(series, false, false);
            var ts = DateTime.Now - start;
            _logger.LogTrace("Saved stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);

            ts = DateTime.Now - initialStart;
            _logger.LogInformation("Finished updating stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        }
    }

    private void UpdateWatchedStats(AnimeSeries series, IReadOnlyList<AnimeEpisode> eps, string name, ref DateTime start)
    {
        _userDataService.UpdateWatchedStats(series, eps);
        var ts = DateTime.Now - start;
        _logger.LogTrace("Updated WATCHED stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        start = DateTime.Now;
    }

    private void UpdateMissingEpisodeStats(AnimeSeries series, IReadOnlyList<AnimeEpisode> eps, string name, ref DateTime start)
    {
        var animeType = series.AniDB_Anime?.AnimeType ?? AnimeType.TVSeries;

        series.MissingEpisodeCount = 0;
        series.MissingEpisodeCountGroups = 0;
        series.HiddenMissingEpisodeCount = 0;
        series.HiddenMissingEpisodeCountGroups = 0;

        // get all the group status records
        var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(series.AniDB_ID);

        // find all the episodes for which the user has a file
        // from this we can determine what their latest episode number is
        // find out which groups the user is collecting

        var latestLocalEpNumber = 0;
        DateTime? lastEpAirDate = null;
        var epReleasedList = new EpisodeList(animeType);
        var epGroupReleasedList = new EpisodeList(animeType);
        var daysofweekcounter = new Dictionary<DayOfWeek, int>();

        var userReleaseGroups = eps
            .Where(a => a.EpisodeType == EpisodeType.Episode)
            .SelectMany(a => a.VideoLocals
                .Select(b => b.ReleaseGroup)
                .WhereNotNull()
                .Where(b => b.Source is "AniDB" && int.TryParse(b.ID, out var groupID) && groupID > 0)
                .Select(b => int.Parse(b.ID))
            )
            .Distinct()
            .ToList();

        var videoLocals = eps.Where(a => a.EpisodeType == EpisodeType.Episode).SelectMany(a =>
                a.VideoLocals.Select(b => new
                {
                    a.AniDB_EpisodeID,
                    VideoLocal = b
                }))
            .ToLookup(a => a.AniDB_EpisodeID, a => a.VideoLocal);

        // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
        eps.AsParallel().Where(a => a.EpisodeType == EpisodeType.Episode).ForAll(ep =>
        {
            var aniEp = ep.AniDB_Episode;
            // Un-aired episodes should not be included in the stats.
            if (aniEp is not { HasAired: true }) return;

            var thisEpNum = aniEp.EpisodeNumber;
            // does this episode have a file released
            var epReleased = false;
            // does this episode have a file released by the group the user is collecting
            var epReleasedGroup = false;

            if (grpStatuses.Count == 0)
            {
                // If there are no group statuses, the UDP command has not been run yet or has failed
                // The current has aired, as un-aired episodes are filtered out above
                epReleased = true;
                // We do not set epReleasedGroup here because we have no way to know
            }
            else
            {
                // Get all groups which have their status set to complete or finished or have released this episode
                var filteredGroups = grpStatuses
                    .Where(
                        a => a.CompletionState is (int)Group_CompletionStatus.Complete or (int)Group_CompletionStatus.Finished
                             || a.HasGroupReleasedEpisode(thisEpNum))
                    .ToList();
                // Episode is released if any of the groups have released it
                epReleased = filteredGroups.Count > 0;
                // Episode is released by one of the groups user is collecting if one of the userReleaseGroups is included in filteredGroups
                epReleasedGroup = filteredGroups.Any(a => userReleaseGroups.Contains(a.GroupID));
            }

            // If epReleased is false, then we consider the episode to be not released, even if it has aired, as no group has released it
            if (!epReleased) return;

            var vids = videoLocals[ep.AniDB_EpisodeID].ToList();

            if (thisEpNum > latestLocalEpNumber && vids.Any())
            {
                latestLocalEpNumber = thisEpNum;
            }

            var airdate = ep.AniDB_Episode?.GetAirDateAsDate();

            // If episode air date is unknown, air date of the anime is used instead
            airdate ??= series.AniDB_Anime?.AirDate;

            // Only count episodes that have already aired
            // airdate could, in theory, only be null here if AniDB neither has information on the episode
            // air date, nor on the anime air date. luckily, as of 2024-07-09, no such case exists.
            if (aniEp.HasAired && airdate is not null)
            {
                // Only convert if we have time info
                DateTime airdateLocal;
                if (airdate.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                {
                    airdateLocal = airdate.Value;
                }
                else
                {
                    airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                    airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal,
                        TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                }

                lock (daysofweekcounter)
                {
                    daysofweekcounter.TryAdd(airdateLocal.DayOfWeek, 0);
                    daysofweekcounter[airdateLocal.DayOfWeek]++;
                }

                if (lastEpAirDate == null || lastEpAirDate < airdate)
                {
                    lastEpAirDate = airdate.Value;
                }
            }

            try
            {
                lock (epReleasedList)
                {
                    epReleasedList.Add(ep, vids.Count > 0);
                }

                // Skip adding to epGroupReleasedList if the episode has not been released by one of the groups user is collecting
                if (!epReleasedGroup) return;

                lock (epGroupReleasedList)
                {
                    epGroupReleasedList.Add(ep, vids.Count > 0);
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Error updating release group stats {Ex}", e);
                throw;
            }
        });

        foreach (var eplst in epReleasedList)
        {
            if (eplst.Available) continue;

            if (eplst.Hidden)
                series.HiddenMissingEpisodeCount++;
            else
                series.MissingEpisodeCount++;
        }

        foreach (var eplst in epGroupReleasedList)
        {
            if (eplst.Available) continue;

            if (eplst.Hidden)
                series.HiddenMissingEpisodeCountGroups++;
            else
                series.MissingEpisodeCountGroups++;
        }

        series.LatestLocalEpisodeNumber = latestLocalEpNumber;
        if (daysofweekcounter.Count > 0)
        {
            series.AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
        }

        series.LatestEpisodeAirDate = lastEpAirDate;

        var ts = DateTime.Now - start;
        _logger.LogTrace("Updated MISSING EPS stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        start = DateTime.Now;
    }

    public Dictionary<AnimeSeries, AniDB_Anime_Staff> SearchSeriesByStaff(string staffName, bool fuzzy = false)
    {
        var allSeries = RepoFactory.AnimeSeries.GetAll();
        var results = new Dictionary<AnimeSeries, AniDB_Anime_Staff>();
        var stringsToSearchFor = new List<string>();
        if (staffName.Contains(' '))
        {
            stringsToSearchFor.AddRange(staffName.Split(' ').GetPermutations()
                .Select(permutation => string.Join(" ", permutation)));
            stringsToSearchFor.Remove(staffName);
            stringsToSearchFor.Insert(0, staffName);
        }
        else
        {
            stringsToSearchFor.Add(staffName);
        }

        foreach (var series in allSeries)
        {
            foreach (var (xref, staff) in RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID).Select(a => (a, a.Creator)))
            {
                if (staff is null)
                    continue;

                foreach (var search in stringsToSearchFor)
                {
                    if (fuzzy)
                    {
                        if (!staff.Name.FuzzyMatch(search))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!staff.Name.Equals(search, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (!results.TryAdd(series, xref))
                    {
                        var comparison = ((int)results[series].RoleType).CompareTo((int)xref.RoleType);
                        if (comparison == 1)
                            results[series] = xref;
                    }

                    goto label0;
                }
            }

            // People hate goto, but this is a legit use for it.
            label0:;
        }

        return results;
    }

    public async Task DeleteSeries(AnimeSeries series, bool deleteFiles, bool updateGroups, bool completelyRemove = false, bool removeFromMylist = true)
    {
        var service = Utils.ServiceContainer.GetRequiredService<IVideoService>();
        foreach (var ep in series.AllAnimeEpisodes)
        {
            foreach (var place in series.VideoLocals.SelectMany(a => a.Places).WhereNotNull())
                await service.DeleteVideoFile(place, removeFile: deleteFiles);

            RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
        }
        RepoFactory.AnimeSeries.Delete(series);

        if (!updateGroups)
        {
            return;
        }

        // finally update stats
        var grp = series.AnimeGroup;
        if (grp is not null)
        {
            if (!grp.AllSeries.Any())
            {
                // Find the topmost group without series
                var parent = grp;
                while (true)
                {
                    var next = parent.Parent;
                    if (next == null || next.AllSeries.Any())
                    {
                        break;
                    }

                    parent = next;
                }

                _groupService.DeleteGroup(parent);
            }
            else
            {
                _groupService.UpdateStatsFromTopLevel(grp, true, true);
            }
        }

        ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Removed);

        if (completelyRemove)
        {
            // episodes, anime, characters, images, staff relations, tag relations, titles
            var images = RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(series.AniDB_ID);
            RepoFactory.AniDB_Anime_PreferredImage.Delete(images);

            var characterXrefs = RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID);
            var characters = characterXrefs
                .Select(x => x.Character)
                .WhereNotNull()
                .Where(x => !x.GetRoles().ExceptBy(characterXrefs.Select(y => y.AniDB_Anime_CharacterID), y => y.AniDB_Anime_CharacterID).Any())
                .ToList();
            RepoFactory.AniDB_Anime_Character.Delete(characterXrefs);
            RepoFactory.AniDB_Character.Delete(characters);

            var actorXrefs = RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID);
            var staffXrefs = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID);
            var creators = actorXrefs.Select(x => x.Creator)
                .Concat(staffXrefs.Select(x => x.Creator))
                .WhereNotNull()
                .Where(x =>
                    !x.Staff.ExceptBy(staffXrefs.Select(y => y.AniDB_Anime_StaffID), y => y.AniDB_Anime_StaffID).Any() &&
                    !x.Characters.ExceptBy(actorXrefs.Select(y => y.AniDB_Anime_Character_CreatorID), y => y.AniDB_Anime_Character_CreatorID).Any()
                )
                .ToList();
            RepoFactory.AniDB_Anime_Character_Creator.Delete(actorXrefs);
            RepoFactory.AniDB_Anime_Staff.Delete(staffXrefs);
            RepoFactory.AniDB_Creator.Delete(creators);

            var tagXrefs = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(series.AniDB_ID);
            RepoFactory.AniDB_Anime_Tag.Delete(tagXrefs);

            var titles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(series.AniDB_ID);
            RepoFactory.AniDB_Anime_Title.Delete(titles);

            var aniDBEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(series.AniDB_ID);
            var episodeTitles = aniDBEpisodes.SelectMany(a => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(a.EpisodeID)).ToList();
            RepoFactory.AniDB_Episode_Title.Delete(episodeTitles);
            RepoFactory.AniDB_Episode.Delete(aniDBEpisodes);

            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(series.AniDB_ID);
            RepoFactory.AniDB_AnimeUpdate.Delete(update);

            // remove all releases linked to this series
            var releases = RepoFactory.StoredReleaseInfo.GetByAnidbAnimeID(series.AniDB_ID);
            foreach (var release in releases)
                await _videoReleaseService.RemoveRelease(release, removeFromMylist);
        }
    }

    /// <summary>
    /// Get the most recent actively watched episode for the user.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="userID">User ID</param>
    /// <param name="includeSpecials">Include specials when searching.</param>
    /// <param name="includeOthers">Include other type episodes when searching.</param>
    /// <returns></returns>
    public AnimeEpisode GetActiveEpisode(AnimeSeries series, int userID, bool includeSpecials = true, bool includeOthers = false)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var order = includeOthers ? new List<EpisodeType> { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special } : null;
        var episodes = series.AnimeEpisodes
            .Select(e => (episode: e, e.AniDB_Episode))
            .Where(tuple =>
                tuple.AniDB_Episode?.EpisodeType is EpisodeType.Episode ||
                (includeSpecials && tuple.AniDB_Episode?.EpisodeType is EpisodeType.Special) ||
                (includeOthers && tuple.AniDB_Episode?.EpisodeType is EpisodeType.Other)
            )
            .OrderBy(tuple => order?.IndexOf(tuple.AniDB_Episode!.EpisodeType) ?? (int?)tuple.AniDB_Episode?.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode?.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        var (episode, _) = episodes
            .SelectMany(episode => episode.VideoLocals.Select(file => (episode, vlUser: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID)!)))
            .Where(tuple => tuple.vlUser is not null)
            .OrderByDescending(tuple => tuple.vlUser.LastUpdated)
            .FirstOrDefault(tuple => tuple.vlUser.ResumePosition > 0);
        return episode;
    }

    #region Next-up Episode(s)

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextUpEpisode"/>.
    /// </summary>
    public class NextUpQuerySingleOptions : NextUpQueryOptions
    {
        /// <summary>
        /// Disable the first episode in the series from showing up.
        /// /// </summary>
        public bool DisableFirstEpisode { get; set; } = false;

        public NextUpQuerySingleOptions() { }

        public NextUpQuerySingleOptions(NextUpQueryOptions options)
        {
            IncludeCurrentlyWatching = options.IncludeCurrentlyWatching;
            IncludeMissing = options.IncludeMissing;
            IncludeUnaired = options.IncludeUnaired;
            IncludeRewatching = options.IncludeRewatching;
            IncludeSpecials = options.IncludeSpecials;
            IncludeOthers = options.IncludeOthers;
        }
    }

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextUpEpisode"/>.
    /// </summary>
    public class NextUpQueryOptions
    {
        /// <summary>
        /// Include currently watching episodes in the search.
        /// </summary>
        public bool IncludeCurrentlyWatching { get; set; } = false;

        /// <summary>
        /// Include missing episodes in the search.
        /// </summary>
        public bool IncludeMissing { get; set; } = false;

        /// <summary>
        /// Include unaired episodes in the search.
        /// </summary>
        public bool IncludeUnaired { get; set; } = false;

        /// <summary>
        /// Include already watched episodes in the search if we determine the
        /// user is "re-watching" the series.
        /// </summary>
        public bool IncludeRewatching { get; set; } = false;

        /// <summary>
        /// Include specials in the search.
        /// </summary>
        public bool IncludeSpecials { get; set; } = true;

        /// <summary>
        /// Include other type episodes in the search.
        /// </summary>
        public bool IncludeOthers { get; set; } = false;
    }

    /// <summary>
    /// Get the next episode for the series for a user.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="userID">User ID</param>
    /// <param name="options">Next-up query options.</param>
    /// <returns></returns>
    public AnimeEpisode? GetNextUpEpisode(AnimeSeries series, int userID, NextUpQuerySingleOptions options)
    {
        var episodeList = series.AnimeEpisodes
            .Select(shoko => (shoko, anidb: shoko.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeType is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeType is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeType is EpisodeType.Other)
                )
            )
            .ToList();

        // Look for active watch sessions and return the episode for the most
        // recent session if found.
        if (options.IncludeCurrentlyWatching)
        {
            var (currentlyWatchingEpisode, _) = episodeList
                .SelectMany(tuple => tuple.shoko.VideoLocals.Select(file => (tuple.shoko, fileUR: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID))))
                .Where(tuple => tuple.fileUR is not null)
                .OrderByDescending(tuple => tuple.fileUR!.LastUpdated)
                .FirstOrDefault(tuple => tuple.fileUR!.ResumePosition > 0);

            if (currentlyWatchingEpisode is not null)
                return currentlyWatchingEpisode;
        }
        // Skip check if there is an active watch session for the series, and we
        // don't allow active watch sessions.
        else if (episodeList.Any(tuple => tuple.shoko.VideoLocals.Any(file => (_vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // If we're listing out other type episodes, then they should be listed
        // before specials, so order them now.
        if (options.IncludeOthers)
        {
            var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
            episodeList = episodeList
                .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeType))
                .ThenBy(tuple => tuple.anidb.EpisodeNumber)
                .ToList();
        }

        // When "re-watching" we look for the next episode after the last
        // watched episode.
        if (options.IncludeRewatching)
        {
            var (lastWatchedEpisode, _) = episodeList
                .SelectMany(tuple => tuple.shoko.VideoLocals.Select(file => (tuple.shoko, fileUR: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID))))
                .Where(tuple => tuple.fileUR is { WatchedDate: not null })
                .OrderByDescending(tuple => tuple.fileUR!.LastUpdated)
                .FirstOrDefault();

            if (lastWatchedEpisode is not null)
            {
                // Return `null` if we're on the last episode in the list.
                var nextIndex = episodeList.FindIndex(tuple => tuple.shoko.AnimeEpisodeID == lastWatchedEpisode.AnimeEpisodeID) + 1;
                if (nextIndex == episodeList.Count)
                    return null;

                var (nextEpisode, _) = episodeList
                    .Skip(nextIndex)
                    .FirstOrDefault(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.shoko.VideoLocals.Count > 0 : tuple => tuple.shoko.VideoLocals.Count > 0);
                return nextEpisode;
            }
        }

        // Find the first episode that's unwatched.
        var (unwatchedEpisode, anidbEpisode) = episodeList
            .Where(tuple =>
            {
                var episodeUserRecord = tuple.shoko.GetUserRecord(userID);
                if (episodeUserRecord is null)
                    return true;

                return !episodeUserRecord.WatchedDate.HasValue;
            })
            .FirstOrDefault(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.shoko.VideoLocals.Count > 0 : tuple => tuple.shoko.VideoLocals.Count > 0);

        // Disable first episode from showing up in the search.
        if (options.DisableFirstEpisode && anidbEpisode is not null && anidbEpisode.EpisodeType is EpisodeType.Episode && anidbEpisode.EpisodeNumber == 1)
            return null;

        return unwatchedEpisode;
    }

    public IReadOnlyList<AnimeEpisode> GetNextUpEpisodes(AnimeSeries series, int userID, NextUpQueryOptions options)
    {
        var firstEpisode = GetNextUpEpisode(series, userID, new(options));
        if (firstEpisode is null)
            return [];

        var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
        var allEpisodes = series.AnimeEpisodes
            .Select(shoko => (shoko, anidb: shoko.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeType is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeType is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeType is EpisodeType.Other)
                )
            )
            .Where(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.shoko.VideoLocals.Count > 0 : tuple => tuple.shoko.VideoLocals.Count > 0)
            .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeType))
            .ThenBy(tuple => tuple.anidb.EpisodeNumber)
            .ToList();
        var index = allEpisodes.FindIndex(tuple => tuple.shoko.AnimeEpisodeID == firstEpisode.AnimeEpisodeID);
        if (index == -1)
            return [];

        return allEpisodes
            .Skip(index)
            .Select(tuple => tuple.shoko)
            .ToList();
    }

    #endregion

    internal class EpisodeList : List<EpisodeList.StatEpisodes>
    {
        public EpisodeList(AnimeType ept)
        {
            AnimeType = ept;
        }

        private AnimeType AnimeType { get; set; }

        private readonly Regex partmatch = new("part (\\d.*?) of (\\d.*)");

        private readonly Regex remsymbols = new("[^A-Za-z0-9 ]");

        private readonly Regex remmultispace = new("\\s+");

        public void Add(AnimeEpisode ep, bool available)
        {
            if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
            {
                var ename = ep.Title;
                var empty = string.IsNullOrEmpty(ename);
                Match? m = null;
                if (!empty)
                {
                    m = partmatch.Match(ename);
                }

                var s = new StatEpisodes.StatEpisode { Available = available, Episode = ep };
                if (m?.Success ?? false)
                {
                    int.TryParse(m.Groups[1].Value, out var _);
                    int.TryParse(m.Groups[2].Value, out var part_count);
                    var rname = partmatch.Replace(ename, string.Empty);
                    rname = remsymbols.Replace(rname, string.Empty);
                    rname = remmultispace.Replace(rname, " ");


                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                    s.PartCount = part_count;
                    s.Match = rname.Trim();
                    if (s.Match == "complete movie" || s.Match == "movie" || s.Match == "ova")
                    {
                        s.Match = string.Empty;
                    }
                }
                else
                {
                    if (empty || ename == "complete movie" || ename == "movie" || ename == "ova")
                    {
                        s.Match = string.Empty;
                    }
                    else
                    {
                        var rname = partmatch.Replace(ep.Title, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");
                        s.Match = rname.Trim();
                    }

                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    s.PartCount = 0;
                }

                StatEpisodes? fnd = null;
                foreach (var k in this)
                {
                    if (k.Any(ss => ss.Match == s.Match)) fnd = k;
                    if (fnd is not null) break;
                }

                if (fnd == null)
                {
                    var eps = new StatEpisodes();
                    eps.Add(s);
                    Add(eps);
                }
                else
                {
                    fnd.Add(s);
                }
            }
            else
            {
                var eps = new StatEpisodes();
                var es = new StatEpisodes.StatEpisode
                {
                    Match = string.Empty,
                    EpisodeType = StatEpisodes.StatEpisode.EpType.Complete,
                    PartCount = 0,
                    Available = available,
                    Episode = ep,
                };
                eps.Add(es);
                Add(eps);
            }
        }

        public class StatEpisodes : List<StatEpisodes.StatEpisode>
        {
            public class StatEpisode
            {
                public enum EpType
                {
                    Complete,
                    Part
                }

                public string? Match { get; set; }
                public int PartCount { get; set; }
                public EpType EpisodeType { get; set; }
                public required bool Available { get; set; }
                public required AnimeEpisode Episode { get; set; }
            }

            public bool Available
            {
                get
                {
                    var maxcnt = this.Select(k => k.PartCount).Concat(new[] { 0 }).Max();
                    var parts = new int[maxcnt + 1];
                    foreach (var k in this)
                    {
                        switch (k.EpisodeType)
                        {
                            case StatEpisode.EpType.Complete when k.Available:
                                return true;
                            case StatEpisode.EpType.Part when k.Available:
                                parts[k.PartCount]++;
                                if (parts[k.PartCount] == k.PartCount)
                                {
                                    return true;
                                }

                                break;
                        }
                    }

                    return false;
                }
            }

            public bool Hidden
                => this.Any(e => e.Episode.IsHidden);
        }
    }
}
