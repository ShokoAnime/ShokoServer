using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Utilities;
using AnimeType = Shoko.Models.Enums.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Match = System.Text.RegularExpressions.Match;

namespace Shoko.Server.Services;

public class AnimeSeriesService
{
    private readonly ILogger<AnimeSeriesService> _logger;
    private readonly AnimeSeries_UserRepository _seriesUsers;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly AniDB_AnimeService _animeService;
    private readonly AnimeGroupService _groupService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly JobFactory _jobFactory;

    public AnimeSeriesService(ILogger<AnimeSeriesService> logger, AnimeSeries_UserRepository seriesUsers, ISchedulerFactory schedulerFactory, JobFactory jobFactory, AniDB_AnimeService animeService, AnimeGroupService groupService, VideoLocal_UserRepository vlUsers)
    {
        _logger = logger;
        _seriesUsers = seriesUsers;
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _animeService = animeService;
        _groupService = groupService;
        _vlUsers = vlUsers;
    }

    public async Task AddSeriesVote(SVR_AnimeSeries series, AniDBVoteType voteType, decimal vote)
    {
        var dbVote = (RepoFactory.AniDB_Vote.GetByEntityAndType(series.AniDB_ID, AniDBVoteType.AnimeTemp) ??
                     RepoFactory.AniDB_Vote.GetByEntityAndType(series.AniDB_ID, AniDBVoteType.Anime)) ??
                     new AniDB_Vote { EntityID = series.AniDB_ID };
        dbVote.VoteValue = (int)Math.Floor(vote * 100);
        dbVote.VoteType = (int)voteType;

        RepoFactory.AniDB_Vote.Save(dbVote);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
            {
                c.AnimeID = series.AniDB_ID;
                c.VoteType = voteType;
                c.VoteValue = Convert.ToDouble(vote);
            }
        );
    }

    public async Task<bool> QueueAniDBRefresh(int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false,
        bool cacheOnly = false, bool skipTmdbUpdate = false)
    {
        if (animeID == 0) return false;
        if (immediate)
        {
            var job = _jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = animeID;
                c.DownloadRelations = downloadRelations;
                c.ForceRefresh = force;
                c.CacheOnly = !force && cacheOnly;
                c.CreateSeriesEntry = createSeriesEntry;
                c.SkipTmdbUpdate = skipTmdbUpdate;
            });

            try
            {
                return await job.Process() != null;
            }
            catch
            {
                return false;
            }
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetAniDBAnimeJob>(c =>
        {
            c.AnimeID = animeID;
            c.DownloadRelations = downloadRelations;
            c.ForceRefresh = force;
            c.CacheOnly = !force && cacheOnly;
            c.CreateSeriesEntry = createSeriesEntry;
            c.SkipTmdbUpdate = skipTmdbUpdate;
        });
        return false;
    }

    public async Task<(bool, Dictionary<SVR_AnimeEpisode, UpdateReason>)> CreateAnimeEpisodes(SVR_AnimeSeries series)
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
            .Select(a => a.VideoLocal?.VideoLocalID)
            .Where(a => a != null)
            .Select(a => a.Value)
            .ToList();

        // queue rescan for the files
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var id in vlIDsToUpdate)
            await scheduler.StartJob<ProcessFileJob>(a => a.VideoLocalID = id);

        _logger.LogTrace($"Generating {anidbEpisodes.Count} episodes for {anime.MainTitle}");

        var one_forth = (int)Math.Round(anidbEpisodes.Count / 4D, 0, MidpointRounding.AwayFromZero);
        var one_half = (int)Math.Round(anidbEpisodes.Count / 2D, 0, MidpointRounding.AwayFromZero);
        var three_forths = (int)Math.Round(anidbEpisodes.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);
        var episodeDict = new Dictionary<SVR_AnimeEpisode, UpdateReason>();
        for (var i = 0; i < anidbEpisodes.Count; i++)
        {
            if (i == one_forth)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 25%");
            }

            if (i == one_half)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 50%");
            }

            if (i == three_forths)
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

    private (SVR_AnimeEpisode episode, bool isNew, bool isUpdated) CreateAnimeEpisode(SVR_AniDB_Episode episode, int animeSeriesID)
    {
        // check if there is an existing episode for this EpisodeID
        var existingEp = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
        var isNew = existingEp is null;
        if (isNew)
            existingEp = new();

        var old = existingEp.DeepClone();
        existingEp.Populate(episode);
        existingEp.AnimeSeriesID = animeSeriesID;

        var updated = !old.Equals(existingEp);
        if (updated)
            RepoFactory.AnimeEpisode.Save(existingEp);

        // We might have removed our AnimeEpisode_User records when wiping out AnimeEpisodes, recreate them if there's watched files
        var vlUsers = existingEp.VideoLocals
            .SelectMany(a => RepoFactory.VideoLocalUser.GetByVideoLocalID(a.VideoLocalID)).ToList();

        // get the list of unique users
        var users = vlUsers.Select(a => a.JMMUserID).Distinct();

        if (vlUsers.Count > 0)
        {
            // per user. An episode is watched if any file is
            foreach (var uid in users)
            {
                // get the last watched file
                var vlUser = vlUsers.Where(a => a.JMMUserID == uid && a.WatchedDate != null)
                    .MaxBy(a => a.WatchedDate);
                // create or update the record
                var epUser = existingEp.GetUserRecord(uid);
                if (epUser != null) continue;

                epUser = new SVR_AnimeEpisode_User(uid, existingEp.AnimeEpisodeID, animeSeriesID)
                {
                    WatchedDate = vlUser?.WatchedDate,
                    PlayedCount = vlUser != null ? 1 : 0,
                    WatchedCount = vlUser != null ? 1 : 0
                };
                RepoFactory.AnimeEpisode_User.Save(epUser);
            }
        }
        else
        {
            // since these are created with VideoLocal_User,
            // these will probably never exist, but if they do, cover our bases
            RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByEpisodeID(existingEp.AnimeEpisodeID));
        }

        return (existingEp, isNew, updated);
    }

    public CL_AnimeSeries_User GetV1UserContract(SVR_AnimeSeries series, int userid)
    {
        if (series == null) return null;
        var contract = new CL_AnimeSeries_User
        {
            AniDB_ID = series.AniDB_ID,
            AnimeGroupID = series.AnimeGroupID,
            AnimeSeriesID = series.AnimeSeriesID,
            DateTimeUpdated = series.DateTimeUpdated,
            DateTimeCreated = series.DateTimeCreated,
            DefaultAudioLanguage = series.DefaultAudioLanguage,
            DefaultSubtitleLanguage = series.DefaultSubtitleLanguage,
            LatestLocalEpisodeNumber = series.LatestLocalEpisodeNumber,
            LatestEpisodeAirDate = series.LatestEpisodeAirDate,
            AirsOn = series.AirsOn,
            EpisodeAddedDate = series.EpisodeAddedDate,
            MissingEpisodeCount = series.MissingEpisodeCount,
            MissingEpisodeCountGroups = series.MissingEpisodeCountGroups,
            SeriesNameOverride = series.SeriesNameOverride,
            DefaultFolder = series.DefaultFolder,
            AniDBAnime = _animeService.GetV1DetailedContract(series.AniDB_Anime),
            CrossRefAniDBTvDBV2 = [],
            TvDB_Series = [],
        };
        if (series.TmdbMovieCrossReferences is { Count: > 0 } tmdbMovieXrefs)
        {
            contract.CrossRefAniDBMovieDB = tmdbMovieXrefs[0].ToClient();
            contract.MovieDB_Movie = tmdbMovieXrefs[0].TmdbMovie?.ToClient();
        }

        contract.CrossRefAniDBMAL = series.MalCrossReferences.ToList();
        try
        {

            var rr = _seriesUsers.GetByUserAndSeriesID(userid, series.AnimeSeriesID);
            if (rr != null)
            {
                contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                contract.WatchedDate = rr.WatchedDate;
                contract.PlayedCount = rr.PlayedCount;
                contract.WatchedCount = rr.WatchedCount;
                contract.StoppedCount = rr.StoppedCount;
                contract.AniDBAnime.AniDBAnime.FormattedTitle = series.PreferredTitle;
                return contract;
            }

            if (contract.AniDBAnime?.AniDBAnime != null)
            {
                contract.AniDBAnime.AniDBAnime.FormattedTitle = series.PreferredTitle;
            }

            return contract;
        }
        catch
        {
            return null;
        }
    }

    public AnimeSeries_User GetOrCreateUserRecord(int seriesID, int userID)
    {
        lock (this)
        {
            var userRecord = _seriesUsers.GetByUserAndSeriesID(userID, seriesID);
            if (userRecord != null)
            {
                return userRecord;
            }

            userRecord = new AnimeSeries_User
            {
                JMMUserID = userID,
                AnimeSeriesID = seriesID
            };
            _seriesUsers.Save(userRecord);
            return userRecord;
        }
    }

    public void MoveSeries(SVR_AnimeSeries series, SVR_AnimeGroup newGroup, bool updateGroupStats = true, bool updateEvent = true)
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
        if (oldGroup != null)
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

    public async Task QueueUpdateStats(SVR_AnimeSeries series)
    {
        if (series == null) return;
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<RefreshAnimeStatsJob>(c => c.AnimeID = series.AniDB_ID);
    }

    public void UpdateStats(SVR_AnimeSeries series, bool watchedStats, bool missingEpsStats)
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
            var eps = series.AllAnimeEpisodes.Where(a => a.AniDB_Episode is not null).ToList();
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

    private void UpdateWatchedStats(SVR_AnimeSeries series, List<SVR_AnimeEpisode> eps, string name, ref DateTime start)
    {
        var vls = RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID)
            .Where(a => !string.IsNullOrEmpty(a?.Hash)).Select(xref =>
                (xref.EpisodeID, xref.VideoLocal))
            .Where(a => a.VideoLocal != null).ToLookup(a => a.EpisodeID, b => b.VideoLocal);
        var vlUsers = vls.SelectMany(
            xref =>
            {
                var users = xref?.SelectMany(a => RepoFactory.VideoLocalUser.GetByVideoLocalID(a.VideoLocalID));
                return users?.Select(a => (EpisodeID: xref.Key, VideoLocalUser: a)) ??
                       Array.Empty<(int EpisodeID, SVR_VideoLocal_User VideoLocalUser)>();
            }
        ).Where(a => a.VideoLocalUser != null).ToLookup(a => (a.EpisodeID, UserID: a.VideoLocalUser.JMMUserID),
            b => b.VideoLocalUser);
        var epUsers = eps.SelectMany(
                ep =>
                {
                    var users = RepoFactory.AnimeEpisode_User.GetByEpisodeID(ep.AnimeEpisodeID);
                    return users.Select(a => (EpisodeID: ep.AniDB_EpisodeID, AnimeEpisode_User: a));
                }
            ).Where(a => a.AnimeEpisode_User != null)
            .ToLookup(a => (a.EpisodeID, UserID: a.AnimeEpisode_User.JMMUserID), b => b.AnimeEpisode_User);

        foreach (var juser in RepoFactory.JMMUser.GetAll())
        {
            var userRecord = GetOrCreateUserRecord(series.AnimeSeriesID, juser.JMMUserID);

            var unwatchedCount = 0;
            var hiddenUnwatchedCount = 0;
            var watchedCount = 0;
            var watchedEpisodeCount = 0;
            DateTime? lastEpisodeUpdate = null;
            DateTime? watchedDate = null;

            var lck = new object();

            eps.AsParallel().Where(ep =>
                vls.Contains(ep.AniDB_EpisodeID) &&
                ep.EpisodeTypeEnum is EpisodeType.Episode or EpisodeType.Special).ForAll(
                ep =>
                {
                    SVR_VideoLocal_User vlUser = null;
                    if (vlUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                    {
                        vlUser = vlUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                            .OrderByDescending(a => a.LastUpdated)
                            .FirstOrDefault(a => a.WatchedDate != null);
                    }

                    var lastUpdated = vlUser?.LastUpdated;

                    SVR_AnimeEpisode_User epUser = null;
                    if (epUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                    {
                        epUser = epUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                            .FirstOrDefault(a => a.WatchedDate != null);
                    }

                    if (vlUser?.WatchedDate == null && epUser?.WatchedDate == null)
                    {
                        if (ep.IsHidden)
                            Interlocked.Increment(ref hiddenUnwatchedCount);
                        else
                            Interlocked.Increment(ref unwatchedCount);
                        return;
                    }

                    lock (lck)
                    {
                        if (vlUser != null)
                        {
                            if (watchedDate == null || (vlUser.WatchedDate != null &&
                                                        vlUser.WatchedDate.Value > watchedDate.Value))
                            {
                                watchedDate = vlUser.WatchedDate;
                            }

                            if (lastEpisodeUpdate == null || lastUpdated.Value > lastEpisodeUpdate.Value)
                            {
                                lastEpisodeUpdate = lastUpdated;
                            }
                        }

                        if (epUser != null)
                        {
                            if (watchedDate == null || (epUser.WatchedDate != null &&
                                                        epUser.WatchedDate.Value > watchedDate.Value))
                            {
                                watchedDate = epUser.WatchedDate;
                            }
                        }
                    }

                    Interlocked.Increment(ref watchedEpisodeCount);
                    Interlocked.Add(ref watchedCount, vlUser?.WatchedCount ?? epUser.WatchedCount);
                });
            userRecord.UnwatchedEpisodeCount = unwatchedCount;
            userRecord.HiddenUnwatchedEpisodeCount = hiddenUnwatchedCount;
            userRecord.WatchedEpisodeCount = watchedEpisodeCount;
            userRecord.WatchedCount = watchedCount;
            userRecord.WatchedDate = watchedDate;
            userRecord.LastEpisodeUpdate = lastEpisodeUpdate;
            RepoFactory.AnimeSeries_User.Save(userRecord);
        }

        var ts = DateTime.Now - start;
        _logger.LogTrace("Updated WATCHED stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        start = DateTime.Now;
    }

    private void UpdateMissingEpisodeStats(SVR_AnimeSeries series, List<SVR_AnimeEpisode> eps, string name, ref DateTime start)
    {
        var animeType = series.AniDB_Anime?.AnimeTypeEnum ?? AnimeType.TVSeries;

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
            .Where(a => a.EpisodeTypeEnum == EpisodeType.Episode)
            .SelectMany(a => a.VideoLocals
                .Select(b => b.ReleaseGroup)
                .WhereNotNull()
                .Where(b => b.ProviderID is "AniDB" && int.TryParse(b.ID, out var groupID) && groupID > 0)
                .Select(b => int.Parse(b.ID))
            )
            .Distinct()
            .ToList();

        var videoLocals = eps.Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).SelectMany(a =>
                a.VideoLocals.Select(b => new
                {
                    a.AniDB_EpisodeID,
                    VideoLocal = b
                }))
            .ToLookup(a => a.AniDB_EpisodeID, a => a.VideoLocal);

        // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
        eps.AsParallel().Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).ForAll(ep =>
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

            var airdate = ep.AniDB_Episode.GetAirDateAsDate();

            // If episode air date is unknown, air date of the anime is used instead
            airdate ??= series.AniDB_Anime?.AirDate;

            // Only count episodes that have already aired
            // airdate could, in theory, only be null here if AniDB neither has information on the episode
            // air date, nor on the anime air date. luckily, as of 2024-07-09, no such case exists.
            if (aniEp.HasAired && airdate != null)
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

    public Dictionary<SVR_AnimeSeries, AniDB_Anime_Staff> SearchSeriesByStaff(string staffName, bool fuzzy = false)
    {
        var allSeries = RepoFactory.AnimeSeries.GetAll();
        var results = new Dictionary<SVR_AnimeSeries, AniDB_Anime_Staff>();
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

            // People hate goto, but this is a legit use for it.
            label0:;
        }

        return results;
    }

    public async Task DeleteSeries(SVR_AnimeSeries series, bool deleteFiles, bool updateGroups, bool completelyRemove = false)
    {
        foreach (var ep in series.AllAnimeEpisodes)
        {
            var service = Utils.ServiceContainer.GetRequiredService<VideoLocal_PlaceService>();
            foreach (var place in series.VideoLocals.SelectMany(a => a.Places).Where(a => a != null))
            {
                if (deleteFiles) await service.RemoveRecordAndDeletePhysicalFile(place);
                else await service.RemoveRecord(place);
            }

            RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
        }
        RepoFactory.AnimeSeries.Delete(series);

        if (!updateGroups)
        {
            return;
        }

        // finally update stats
        var grp = series.AnimeGroup;
        if (grp != null)
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
    public SVR_AnimeEpisode GetActiveEpisode(SVR_AnimeSeries series, int userID, bool includeSpecials = true, bool includeOthers = false)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var order = includeOthers ? new List<EpisodeType> { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special } : null;
        var episodes = series.AnimeEpisodes
            .Select(e => (episode: e, e.AniDB_Episode))
            .Where(tuple => tuple.AniDB_Episode.EpisodeTypeEnum is EpisodeType.Episode || (includeSpecials && tuple.AniDB_Episode.EpisodeTypeEnum is EpisodeType.Special) || (includeOthers && tuple.AniDB_Episode.EpisodeTypeEnum is EpisodeType.Other))
            .OrderBy(tuple => order?.IndexOf(tuple.AniDB_Episode.EpisodeTypeEnum) ?? tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        var (episode, _) = episodes
            .SelectMany(episode => episode.VideoLocals.Select(file => (episode, _vlUsers.GetByUserIDAndVideoLocalID(userID, file.VideoLocalID))))
            .Where(tuple => tuple.Item2 is not null)
            .OrderByDescending(tuple => tuple.Item2.LastUpdated)
            .FirstOrDefault(tuple => tuple.Item2.ResumePosition > 0);
        return episode;
    }

    #region Next-up Episode(s)
#nullable enable

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
    public SVR_AnimeEpisode? GetNextUpEpisode(SVR_AnimeSeries series, int userID, NextUpQuerySingleOptions options)
    {
        var episodeList = series.AnimeEpisodes
            .Select(shoko => (shoko, anidb: shoko.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeTypeEnum is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeTypeEnum is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeTypeEnum is EpisodeType.Other)
                )
            )
            .ToList();

        // Look for active watch sessions and return the episode for the most
        // recent session if found.
        if (options.IncludeCurrentlyWatching)
        {
            var (currentlyWatchingEpisode, _) = episodeList
                .SelectMany(tuple => tuple.shoko.VideoLocals.Select(file => (tuple.shoko, fileUR: _vlUsers.GetByUserIDAndVideoLocalID(userID, file.VideoLocalID))))
                .Where(tuple => tuple.fileUR is not null)
                .OrderByDescending(tuple => tuple.fileUR!.LastUpdated)
                .FirstOrDefault(tuple => tuple.fileUR!.ResumePosition > 0);

            if (currentlyWatchingEpisode is not null)
                return currentlyWatchingEpisode;
        }
        // Skip check if there is an active watch session for the series, and we
        // don't allow active watch sessions.
        else if (episodeList.Any(tuple => tuple.shoko.VideoLocals.Any(file => (_vlUsers.GetByUserIDAndVideoLocalID(userID, file.VideoLocalID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // If we're listing out other type episodes, then they should be listed
        // before specials, so order them now.
        if (options.IncludeOthers)
        {
            var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
            episodeList = episodeList
                .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeTypeEnum))
                .ThenBy(tuple => tuple.anidb.EpisodeNumber)
                .ToList();
        }

        // When "re-watching" we look for the next episode after the last
        // watched episode.
        if (options.IncludeRewatching)
        {
            var (lastWatchedEpisode, _) = episodeList
                .SelectMany(tuple => tuple.shoko.VideoLocals.Select(file => (tuple.shoko, fileUR: _vlUsers.GetByUserIDAndVideoLocalID(userID, file.VideoLocalID))))
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
        if (options.DisableFirstEpisode && anidbEpisode is not null && anidbEpisode.EpisodeType == (int)EpisodeType.Episode && anidbEpisode.EpisodeNumber == 1)
            return null;

        return unwatchedEpisode;
    }

    public IReadOnlyList<SVR_AnimeEpisode> GetNextUpEpisodes(SVR_AnimeSeries series, int userID, NextUpQueryOptions options)
    {
        var firstEpisode = GetNextUpEpisode(series, userID, new(options));
        if (firstEpisode is null)
            return [];

        var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
        var allEpisodes = series.AnimeEpisodes
            .Select(shoko => (shoko, anidb: shoko.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeTypeEnum is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeTypeEnum is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeTypeEnum is EpisodeType.Other)
                )
            )
            .Where(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.shoko.VideoLocals.Count > 0 : tuple => tuple.shoko.VideoLocals.Count > 0)
            .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeTypeEnum))
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

#nullable disable
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

        public void Add(SVR_AnimeEpisode ep, bool available)
        {
            if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
            {
                var ename = ep.PreferredTitle;
                var empty = string.IsNullOrEmpty(ename);
                Match m = null;
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
                        var rname = partmatch.Replace(ep.PreferredTitle, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");
                        s.Match = rname.Trim();
                    }

                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    s.PartCount = 0;
                }

                StatEpisodes fnd = null;
                foreach (var k in this)
                {
                    if (k.Any(ss => ss.Match == s.Match)) fnd = k;
                    if (fnd != null) break;
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

                public string Match;
                public int PartCount;
                public EpType EpisodeType { get; set; }
                public bool Available { get; set; }
                public SVR_AnimeEpisode Episode { get; set; }
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
