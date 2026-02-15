using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Legacy;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v1.Implementations;

public partial class ShokoServiceImplementation
{
    #region Episodes and Files

    /// <summary>
    /// Finds the previous episode for use int the next unwatched episode.
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="userID"></param>
    /// <returns></returns>
    [HttpGet("Episode/PreviousEpisode/{animeSeriesID}/{userID}")]
    public CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
    {
        try
        {
            var nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
            if (nextEp is null)
                return null;

            var epType = nextEp.EpisodeType;
            var epNum = nextEp.EpisodeNumber - 1;

            if (epNum <= 0)
                return null;

            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series is null)
                return null;

            var anidbEpisodes = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, (EpisodeType)epType, epNum);
            if (anidbEpisodes.Count == 0)
                return null;

            var ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidbEpisodes[0].EpisodeID);
            return _legacyV1Service.GetV1Contract(ep, userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Episode/NextForSeries/{animeSeriesID}/{userID}")]
    public CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID)
    {
        try
        {
            var seriesService = Utils.ServiceContainer.GetService<AnimeSeriesService>();
            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series is null)
                return null;

            var episode = seriesService.GetNextUpEpisode(series, userID, new());
            if (episode is null)
                return null;

            return _legacyV1Service.GetV1Contract(episode, userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Episode/Unwatched/{animeSeriesID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
    {
        var ret = new List<CL_AnimeEpisode_User>();

        try
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                .Where(a => a != null && !a.IsHidden)
                .Select(a => _legacyV1Service.GetV1Contract(a, userID))
                .WhereNotNull()
                .Where(a => a.WatchedCount == 0)
                .OrderBy(a => a.EpisodeType)
                .ThenBy(a => a.EpisodeNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ret;
        }
    }

    [HttpGet("Episode/NextForGroup/{animeGroupID}/{userID}")]
    public CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
    {
        try
        {
            var group = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (group is null)
            {
                return null;
            }

            var allSeries = group.AllSeries.OrderBy(a => a.AirDate).ToList();


            foreach (var ser in allSeries)
            {
                var contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                if (contract != null)
                {
                    return contract;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Episode/ContinueWatching/{userID}/{maxRecords}")]
    public List<CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null) return retEps;

            // find the locked Continue Watching Filter
            var lockedGFs = RepoFactory.FilterPreset.GetLockedGroupFilters();
            var gf = lockedGFs?.FirstOrDefault(a => a.Name == "Continue Watching");
            if (gf is null) return retEps;

            var evaluator = HttpContext.RequestServices.GetRequiredService<IFilterEvaluator>();
            var groupService = HttpContext.RequestServices.GetRequiredService<AnimeGroupService>();
            var comboGroups = evaluator.EvaluateFilter(gf, user).Select(a => RepoFactory.AnimeGroup.GetByID(a.Key)).WhereNotNull()
                .Select(a => groupService.GetV1Contract(a, userID));

            foreach (var group in comboGroups)
            {
                var seriesList = RepoFactory.AnimeSeries.GetByGroupID(group.AnimeGroupID).OrderBy(a => a.AirDate).ToList();
                var seriesWatching = new List<int>();
                foreach (var ser in seriesList)
                {
                    if (!user.AllowedSeries(ser)) continue;

                    var anime = ser.AniDB_Anime;
                    var useSeries = seriesWatching.Count == 0 || anime.AnimeType is not AnimeType.TVSeries || !anime.RelatedAnime.Any(a =>
                        a.RelationType.ToLower().Trim().Equals("sequel") || a.RelationType.ToLower().Trim().Equals("prequel"));
                    if (!useSeries) continue;

                    var ep = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (ep is null) continue;

                    retEps.Add(ep);

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords) return retEps;

                    if (anime.AnimeType is AnimeType.TVSeries) seriesWatching.Add(ser.AniDB_ID);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retEps;
    }

    /// <summary>
    ///     Gets a list of episodes watched based on the most recently watched series
    ///     It will return the next episode to watch in the most recent 10 series
    /// </summary>
    /// <returns></returns>
    [HttpGet("Episode/WatchedToWatch/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var start = DateTime.Now;

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retEps;
            }

            // get a list of series that is applicable
            var allSeriesUser = RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(userID);

            var ts = DateTime.Now - start;
            _logger.LogInformation("GetEpisodesToWatch_RecentlyWatched:Series: {Milliseconds}", ts.TotalMilliseconds);
            start = DateTime.Now;

            foreach (var userRecord in allSeriesUser)
            {
                var series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                if (series is null)
                {
                    continue;
                }

                if (!user.AllowedSeries(series))
                {
                    continue;
                }

                var ep = GetNextUnwatchedEpisode(userRecord.AnimeSeriesID, userID);
                if (ep != null)
                {
                    retEps.Add(ep);

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords)
                    {
                        ts = DateTime.Now - start;
                        _logger.LogInformation("GetEpisodesToWatch_RecentlyWatched:Episodes: {Milliseconds}", ts.TotalMilliseconds);
                        return retEps;
                    }
                }
            }

            ts = DateTime.Now - start;
            _logger.LogInformation("GetEpisodesToWatch_RecentlyWatched:Episodes: {Milliseconds}", ts.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retEps;
    }

    [HttpGet("Episode/Watched/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            return
                RepoFactory.AnimeEpisode_User.GetMostRecentlyWatched(userID, maxRecords)
                    .Select(a => _legacyV1Service.GetV1Contract(RepoFactory.AnimeEpisode.GetByID(a.AnimeEpisodeID), userID))
                    .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retEps;
    }

    [HttpGet("Episode/RecentlyAdded/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retEps;
            }

            // We will deal with a large list, don't perform ops on the whole thing!
            var vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords * 5, userID);
            foreach (var vid in vids)
            {
                if (string.IsNullOrEmpty(vid.Hash)) continue;

                foreach (var ep in vid.AnimeEpisodes)
                {
                    var epContract = _legacyV1Service.GetV1Contract(ep, userID);
                    if (!user.AllowedSeries(ep.AnimeSeries)) continue;
                    retEps.Add(epContract);

                    // Let's only return the specified amount
                    if (retEps.Count < maxRecords) continue;
                    return retEps;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retEps;
    }

    [HttpGet("Episode/RecentlyAdded/Summary/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retEps;
            }

            var start = DateTime.Now;

            var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(-1, userID)
                .SelectMany(a => a.AnimeEpisodes).Select(a => a.AnimeSeriesID).Distinct().Take(maxRecords);


            var ts2 = DateTime.Now - start;
            _logger.LogInformation("GetEpisodesRecentlyAddedSummary:RawData in {Milliseconds} ms", ts2.TotalMilliseconds);
            start = DateTime.Now;

            foreach (var res in results)
            {
                var ser = RepoFactory.AnimeSeries.GetByID(res);
                if (ser is null)
                {
                    continue;
                }

                if (!user.AllowedSeries(ser))
                {
                    continue;
                }

                var vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                if (vids.Count == 0)
                {
                    continue;
                }

                var eps = vids[0].AnimeEpisodes;
                if (eps.Count == 0)
                {
                    continue;
                }

                var epContract = _legacyV1Service.GetV1Contract(eps[0], userID);
                if (epContract != null)
                {
                    retEps.Add(epContract);

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords)
                    {
                        ts2 = DateTime.Now - start;
                        _logger.LogInformation("GetEpisodesRecentlyAddedSummary:Episodes in {Time} ms", ts2.TotalMilliseconds);
                        return retEps;
                    }
                }
            }

            ts2 = DateTime.Now - start;
            _logger.LogInformation("GetEpisodesRecentlyAddedSummary:Episodes in {Time} ms", ts2.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retEps;
    }

    [HttpGet("Series/RecentlyAdded/{maxRecords}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int userID)
    {
        var retSeries = new List<CL_AnimeSeries_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retSeries;
            }

            var series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(maxRecords, userID);
            retSeries.AddRange(series.Select(a => _legacyV1Service.GetV1UserContract(a, userID)).WhereNotNull());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retSeries;
    }

    [HttpGet("Episode/LastWatched/{animeSeriesID}/{userID}")]
    public CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int userID)
    {
        try
        {
            return _legacyV1Service.GetV1Contract(RepoFactory.AnimeEpisode_User.GetLastWatchedEpisodeForSeries(animeSeriesID, userID)?.AnimeEpisode, userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpGet("Episode/{animeEpisodeID}/{userID}")]
    public CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID)
    {
        try
        {
            return _legacyV1Service.GetV1Contract(RepoFactory.AnimeEpisode.GetByID(animeEpisodeID), userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [NonAction]
    public IReadOnlyList<CL_AnimeEpisode> GetAllEpisodes()
    {
        try
        {
            return RepoFactory.AnimeEpisode.GetAll()
                .Select(a => (CL_AnimeEpisode)_legacyV1Service.GetV1Contract(a, 0))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Episode/AniDB/{episodeID}/{userID}")]
    public CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
    {
        try
        {
            return _legacyV1Service.GetV1Contract(RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episodeID), userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpDelete("File/Association/{videoLocalID}/{animeEpisodeID}")]
    public string RemoveAssociationOnFile(int videoLocalID, int animeEpisodeID)
    {
        return "All file association/deassociation actions are deprecated in APIv1";
    }

    [HttpPost("File/Status/{videoLocalID}/{isIgnored}")]
    public string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
            {
                return "Could not find video record";
            }

            vid.IsIgnored = isIgnored;
            RepoFactory.VideoLocal.Save(vid, false);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("File/Variation/{videoLocalID}/{isVariation}")]
    public string SetVariationStatusOnFile(int videoLocalID, bool isVariation)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
            {
                return "Could not find video record";
            }

            vid.IsVariation = isVariation;
            RepoFactory.VideoLocal.Save(vid, false);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("File/Association/{videoLocalID}/{animeEpisodeID}")]
    public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
    {
        return "All file association/deassociation actions are deprecated in APIv1";
    }

    [HttpPost("File/Association/{videoLocalID}/{animeSeriesID}/{startingEpisodeNumber}/{endEpisodeNumber}")]
    public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID,
        int startingEpisodeNumber,
        int endEpisodeNumber)
    {
        return "All file association/deassociation actions are deprecated in APIv1";
    }

    [HttpPost("File/Association/{animeSeriesID}/{startingEpisodeNumber}/{singleEpisode}")]
    public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, string startingEpisodeNumber,
        bool singleEpisode)
    {
        return "All file association/deassociation actions are deprecated in APIv1";
    }

    [HttpPost("AniDB/Refresh/{missingInfo}/{outOfDate}/{countOnly}")]
    public int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
    {
        try
        {
            return _actionService.UpdateAnidbReleaseInfo(countOnly).Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return 0;
        }
    }

    [HttpPost("File/Refresh/{videoLocalID}")]
    public string UpdateFileData(int videoLocalID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
                return "File could not be found";

            if (!_videoReleaseService.AutoMatchEnabled)
                return "Release auto-matching is currently disabled";

            _videoReleaseService.ScheduleFindReleaseForVideo(vid, force: true, prioritize: true).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("File/Rescan/{videoLocalID}")]
    public string RescanFile(int videoLocalID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
                return "File could not be found";

            if (string.IsNullOrEmpty(vid.Hash))
                return "Could not Update a cloud file without hash, hash it locally first";

            if (!_videoReleaseService.AutoMatchEnabled)
                return "Release auto-matching is currently disabled";

            _videoReleaseService.ScheduleFindReleaseForVideo(vid, force: true, prioritize: true).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("File/Rehash/{videoLocalID}")]
    public void RehashFile(int videoLocalID)
    {
        var vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

        if (vl != null)
        {
            var pl = vl.FirstResolvedPlace;
            if (pl is null)
            {
                _logger.LogError("Unable to hash videolocal with id = {VideoLocalID}, it has no assigned place", videoLocalID);
                return;
            }

            var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
            scheduler.StartJob<HashFileJob>(
                c => (c.FilePath, c.ForceHash) = (pl.Path, true),
                prioritize: true
            ).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    ///     Deletes the VideoLocal record and the associated physical file
    /// </summary>
    /// <param name="videoplaceid"></param>
    /// <returns></returns>
    [HttpDelete("File/Physical/{videoplaceid}")]
    public string DeleteVideoLocalPlaceAndFile(int videoplaceid)
    {
        try
        {
            var place = RepoFactory.VideoLocalPlace.GetByID(videoplaceid);
            if (place?.VideoLocal is null)
            {
                return "Database entry does not exist";
            }

            _videoService.DeleteVideoFile(place, true).GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    /// <summary>
    ///     Deletes the VideoLocal record and the associated physical file
    /// </summary>
    /// <param name="videoplaceid"></param>
    /// <returns></returns>
    [HttpDelete("File/Physical/{videoplaceid}/SkipFolder")]
    public string DeleteVideoLocalPlaceAndFileSkipFolder(int videoplaceid)
    {
        try
        {
            var place = RepoFactory.VideoLocalPlace.GetByID(videoplaceid);
            if (place?.VideoLocal is null)
            {
                return "Database entry does not exist";
            }

            _videoService.DeleteVideoFile(place, true, removeFolders: false).GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("File/Resume/{videoLocalID}/{resumeposition}/{userID}")]
    public string SetResumePosition(int videoLocalID, long resumeposition, int userID)
    {
        try
        {
            var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (video is null)
                return "Could not find video local record";

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
                return "Could not find user record";

            var videoUserData = _userDataService.GetVideoUserData(video, user);
            _userDataService.SaveVideoUserData(
                video,
                user,
                new(videoUserData)
                {
                    ProgressPosition = TimeSpan.FromTicks(resumeposition),
                    LastUpdatedAt = DateTime.Now
                }
            ).GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("File/ForAnime/{animeID}/{userID}")]
    public List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
    {
        try
        {
            // Try sorted first, then try unsorted if failed
            var list = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                    a?.Places?.FirstOrDefault(b => !string.IsNullOrEmpty(b.Path))?.Path != null)
                .DistinctBy(a => a?.Places?.FirstOrDefault()?.Path)
                .ToList();
            list.Sort(FileQualityFilter.CompareTo);
            return list.Select(a => _legacyV1Service.GetV1Contract(a, userID)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            try
            {
                // Two checks because the Where doesn't guarantee that First will not be null, only that a not-null value exists
                var list = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                        a?.Places?.FirstOrDefault(b => !string.IsNullOrEmpty(b.Path))?.Path != null)
                    .DistinctBy(a => a?.Places?.FirstOrDefault()?.Path)
                    .Select(a => _legacyV1Service.GetV1Contract(a, userID))
                    .ToList();
                return list;
            }
            catch
            {
                // Ignore
            }
        }

        return new List<CL_VideoLocal>();
    }

    [HttpGet("AniDB/Vote/{animeID}")]
    public CL_AniDB_Vote GetUserVote(int animeID)
    {
        try
        {
            var user = HttpContext.GetUser();
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (series is null)
                return null;

            var userData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(user.JMMUserID, series.AnimeSeriesID);
            if (userData is not { HasUserRating: true })
                return null;

            return new()
            {
                AniDB_VoteID = animeID,
                EntityID = animeID,
                VoteType = (int)userData.UserRatingVoteType.Value,
                VoteValue = userData.AbsoluteUserRating.Value,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpGet("Episode/IncrementStats/{animeEpisodeID}/{userID}/{statCountType}")]
    public void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType)
    {
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return;
            }

            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep is null)
            {
                return;
            }

            _userDataService.IncrementEpisodeStats(ep, user, statCountType)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpDelete("AniDB/MyList/{fileID}")]
    public void DeleteFileFromMyList(int fileID)
    {
        var vl = RepoFactory.VideoLocal.GetByMyListID(fileID);
        if (vl is null)
        {
            return;
        }

        var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.StartJob<DeleteFileFromMyListJob>(
            c => (c.Hash, c.FileSize) = (vl.Hash, vl.FileSize),
            prioritize: true
        ).GetAwaiter().GetResult();
    }

    [HttpPost("AniDB/MyList/{hash}")]
    public void ForceAddFileToMyList(string hash)
    {
        try
        {
            var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
            scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = hash, prioritize: true).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpGet("AniDB/Episode/ForAnime/{animeID}")]
    public List<CL_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
    {
        try
        {
            return RepoFactory.AniDB_Episode.GetByAnimeID(animeID)
                .Select(a => a.ToClient())
                .OrderBy(a => a.EpisodeType)
                .ThenBy(a => a.EpisodeNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_AniDB_Episode>();
    }

    [HttpGet("Episode/ForSeries/{animeSeriesID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID)
    {
        var eps = new List<CL_AnimeEpisode_User>();
        try
        {
            return
                RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                    .Where(a => a != null && !a.IsHidden)
                    .Select(a => _legacyV1Service.GetV1Contract(a, userID))
                    .WhereNotNull()
                    .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return eps;
    }

    [HttpGet("Episode/Old/{animeSeriesID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID)
    {
        var eps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(1) ??
                       RepoFactory.JMMUser.GetAll().FirstOrDefault(a => a.Username == "Default");
            //HACK (We should have a default user locked)
            if (user != null)
            {
                return GetEpisodesForSeries(animeSeriesID, user.JMMUserID);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return eps;
    }

    [HttpGet("File/Detailed/{episodeID}/{userID}")]
    public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
    {
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep != null)
            {
                var files = ep.VideoLocals.ToList();
                files.Sort(FileQualityFilter.CompareTo);
                return files.Select(a => _legacyV1Service.GetV1DetailedContract(a, userID)).ToList();
            }

            return new List<CL_VideoDetailed>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_VideoDetailed>();
    }

    [HttpGet("File/ForEpisode/{episodeID}/{userID}")]
    public List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
    {
        var contracts = new List<CL_VideoLocal>();
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep != null)
            {
                foreach (var vid in ep.VideoLocals)
                {
                    contracts.Add(_legacyV1Service.GetV1Contract(vid, userID));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return contracts;
    }

    [HttpPost("File/Watch/{videoLocalID}/{watchedStatus}/{userID}")]
    public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
    {
        try
        {
            var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (video is null)
                return "Could not find video local record";

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
                return "Could not find user record";

            _userDataService.SetVideoWatchedStatus(video, user, watchedStatus).GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}")]
    public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus,
        int userID)
    {
        var response = new CL_Response<CL_AnimeEpisode_User> { ErrorMessage = "", Result = null };
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep is null)
            {
                response.ErrorMessage = "Could not find anime episode record";
                return response;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                response.ErrorMessage = "Could not find user record";
                return response;
            }

            _userDataService.SetEpisodeWatchedStatus(ep, user, watchedStatus, DateTime.Now).GetAwaiter().GetResult();
            var series = ep.AnimeSeries;
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            seriesService.UpdateStats(series, true, false);
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, true, true);

            // refresh from db

            response.Result = _legacyV1Service.GetV1Contract(ep, userID);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            response.ErrorMessage = ex.Message;
            return response;
        }
    }

    [HttpPost("File/Detailed/{videoLocalID}/{userID}")]
    public CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
            {
                return null;
            }

            return _legacyV1Service.GetV1DetailedContract(vid, userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Episode/ForSingleFile/{videoLocalID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID)
    {
        var contracts = new List<CL_AnimeEpisode_User>();
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid is null)
            {
                return contracts;
            }

            foreach (var ep in vid.AnimeEpisodes)
            {
                var eps = _legacyV1Service.GetV1Contract(ep, userID);
                if (eps != null)
                {
                    contracts.Add(eps);
                }
            }

            return contracts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return contracts;
        }
    }

    /// <summary>
    ///     Get all the release groups for an episode for which the user is collecting
    /// </summary>
    /// <param name="aniDBEpisodeID"></param>
    /// <returns></returns>
    [HttpGet("AniDB/ReleaseGroup/FromEpisode/{aniDBEpisodeID}")]
    public List<CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
    {
        var start = DateTime.Now;

        var relGroups = new List<CL_AniDB_GroupStatus>();

        try
        {
            var aniEp = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);
            if (aniEp is null)
            {
                return relGroups;
            }

            if (aniEp.EpisodeType is not EpisodeType.Episode)
            {
                return relGroups;
            }

            var series = RepoFactory.AnimeSeries.GetByAnimeID(aniEp.AnimeID);
            if (series is null)
            {
                return relGroups;
            }

            // get a list of all the release groups the user is collecting
            var userReleaseGroups = new Dictionary<int, int>();
            foreach (var ep in series.AllAnimeEpisodes)
            {
                var vids = ep.VideoLocals;
                var hashes = vids.Select(a => a.Hash).Distinct().ToList();
                foreach (var s in hashes)
                {
                    var vid = vids.First(a => a.Hash == s);
                    if (vid.ReleaseGroup is { Source: "AniDB" } group && int.TryParse(group.ID, out var groupId))
                    {
                        if (!userReleaseGroups.ContainsKey(groupId))
                            userReleaseGroups[groupId] = 0;

                        userReleaseGroups[groupId] = userReleaseGroups[groupId] + 1;
                    }
                }
            }

            // get all the release groups for this series
            var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(aniEp.AnimeID);
            foreach (var gs in grpStatuses)
            {
                if (userReleaseGroups.ContainsKey(gs.GroupID))
                {
                    if (gs.HasGroupReleasedEpisode(aniEp.EpisodeNumber))
                    {
                        var cl = gs.ToClient();
                        cl.UserCollecting = true;
                        cl.FileCount = userReleaseGroups[gs.GroupID];
                        relGroups.Add(cl);
                    }
                }
            }

            var ts = DateTime.Now - start;
            _logger.LogInformation("GetMyReleaseGroupsForAniDBEpisode  in {Milli} ms", ts.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return relGroups;
    }

    #endregion

    #region Groups and Series

    [HttpGet("Series/{animeSeriesID}/{userID}")]
    public CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID)
    {
        try
        {
            return _legacyV1Service.GetV1UserContract(RepoFactory.AnimeSeries.GetByID(animeSeriesID), userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [NonAction]
    public List<CL_AnimeSeries_User> GetSeriesByFolderID(int FolderID, int userID, int max)
    {
        try
        {
            var limit = 0;
            var list = new List<CL_AnimeSeries_User>();

            foreach (var vi in RepoFactory.VideoLocal.GetByManagedFolderID(FolderID))
            {
                foreach (var ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                {
                    var ase = GetSeries(ae.AnimeSeriesID, userID);
                    if (!list.Contains(ase))
                    {
                        limit++;
                        list.Add(ase);
                        if (limit >= max)
                        {
                            break;
                        }
                    }
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_AnimeSeries_User>();
    }

    /// <summary>
    /// </summary>
    /// <param name="animeID"></param>
    /// <param name="voteValue"></param>
    /// <param name="voteType">Must be 1 or 2 (Anime or Anime Temp)</param>
    [HttpPost("AniDB/Vote/{animeID}/{voteType}")]
    public async void VoteAnime(int animeID, [FromForm] double voteValue, int voteType)
    {
        var series = animeID is > 0 ? RepoFactory.AnimeSeries.GetByAnimeID(animeID) : null;
        var user = HttpContext.GetUser();
        var pluginVoteType = voteType is 1 ? SeriesVoteType.Permanent : SeriesVoteType.Temporary;
        await _userDataService.RateSeries(series, user, voteValue, pluginVoteType);
    }

    [HttpDelete("AniDB/Vote/{animeID}")]
    public async void VoteAnimeRevoke(int animeID)
    {
        var series = animeID is > 0 ? RepoFactory.AnimeSeries.GetByAnimeID(animeID) : null;
        var user = HttpContext.GetUser();
        await _userDataService.UnrateSeries(series, user);
    }

    /// <summary>
    ///     Set watched status on all normal episodes
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="watchedStatus"></param>
    /// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
    /// <param name="episodeType"></param>
    /// <param name="userID"></param>
    /// <returns></returns>
    [HttpPost("Series/Watch/{animeSeriesID}/{watchedStatus}/{maxEpisodeNumber}/{episodeType:int}/{userID}")]
    public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType,
        int userID)
    {
        try
        {
            var eps = RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID);

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
                return "Could not find user record";

            AnimeSeries ser = null;
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            foreach (var ep in eps)
            {
                if (ep?.AniDB_Episode is null)
                {
                    continue;
                }

                if (ep.EpisodeType == (EpisodeType)episodeType &&
                    ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
                {
                    // check if this episode is already watched
                    var currentStatus = false;
                    var epUser = ep.GetUserRecord(userID);
                    if (epUser != null)
                    {
                        currentStatus = epUser.IsWatched;
                    }

                    if (currentStatus != watchedStatus)
                    {
                        _logger.LogInformation("Updating episode: {Num} to {Watched}", ep.AniDB_Episode.EpisodeNumber, watchedStatus);
                        _userDataService.SetEpisodeWatchedStatus(ep, user, watchedStatus, updateStatsNow: false).GetAwaiter().GetResult();
                    }
                }


                ser = ep.AnimeSeries;
            }

            // now update the stats
            if (ser != null)
            {
                seriesService.UpdateStats(ser, true, true);
                var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
                groupService.UpdateStatsFromTopLevel(ser.AnimeGroup?.TopLevelAnimeGroup, true, true);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("Series/ForAnime/{animeID}/{userID}")]
    public CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID)
    {
        try
        {
            return _legacyV1Service.GetV1UserContract(RepoFactory.AnimeSeries.GetByAnimeID(animeID), userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpGet("Series/ExistingForAnime/{animeID}")]
    public bool GetSeriesExistingForAnime(int animeID)
    {
        try
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (series is null)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return true;
    }

    [HttpGet("Group/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroups(int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            return RepoFactory.AnimeGroup.GetAll()
                .Select(a => groupService.GetV1Contract(a, userID))
                .OrderBy(a => a.GroupName)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return grps;
    }

    [HttpGet("Group/AboveGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            int? grpid = animeGroupID;
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            while (grpid.HasValue)
            {
                grpid = null;
                var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp != null)
                {
                    grps.Add(groupService.GetV1Contract(grp, userID));
                    grpid = grp.AnimeGroupParentID;
                }
            }

            return grps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return grps;
    }

    [HttpGet("Group/AboveSeries/{animeSeriesID}/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series is null)
            {
                return grps;
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            foreach (var grp in series.AllGroupsAbove)
            {
                grps.Add(groupService.GetV1Contract(grp, userID));
            }

            return grps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return grps;
    }

    [HttpGet("Group/{animeGroupID}/{userID}")]
    public CL_AnimeGroup_User GetGroup(int animeGroupID, int userID)
    {
        try
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            return groupService.GetV1Contract(RepoFactory.AnimeGroup.GetByID(animeGroupID), userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpPost("Group/Recreate/{resume}")]
    public void RecreateAllGroups(bool resume = false)
    {
        try
        {
            _groupCreator.RecreateAllGroups().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpPost("Group/Rename")]
    public string RenameAllGroups()
    {
        try
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.RenameAllGroups();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpDelete("Group/{animeGroupID}/{deleteFiles}")]
    public string DeleteAnimeGroup(int animeGroupID, bool deleteFiles)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return "Group does not exist";
            }

            if (grp.AllSeries.Count != 0)
            {
                return "Group must be empty to be deleted. Move the series out of the group first.";
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.DeleteGroup(grp);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("Group/ForFilter/{groupFilterID}/{userID}/{getSingleSeriesGroups}")]
    public List<CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID, bool getSingleSeriesGroups)
    {
        var retGroups = new List<CL_AnimeGroup_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null) return retGroups;

            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);

            if (gf != null)
            {
                var evaluator = HttpContext.RequestServices.GetRequiredService<IFilterEvaluator>();
                var results = evaluator.EvaluateFilter(gf, user);
                var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
                retGroups = results.Select(a => RepoFactory.AnimeGroup.GetByID(a.Key)).WhereNotNull().Select(a => groupService.GetV1Contract(a, userID))
                    .ToList();
            }

            if (!getSingleSeriesGroups) return retGroups;

            var nGroups = new List<CL_AnimeGroup_User>();
            foreach (var cag in retGroups)
            {
                var ng = cag.DeepCopy();
                if (cag.Stat_SeriesCount == 1)
                {
                    if (cag.DefaultAnimeSeriesID.HasValue)
                    {
                        ng.SeriesForNameOverride = _legacyV1Service.GetV1UserContract(RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                            .FirstOrDefault(a => a.AnimeSeriesID == cag.DefaultAnimeSeriesID.Value), userID);
                    }

                    ng.SeriesForNameOverride ??= _legacyV1Service.GetV1UserContract(RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID).FirstOrDefault(), userID);
                }

                nGroups.Add(ng);
            }

            return nGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retGroups;
    }

    [HttpPost("Group/{userID}")]
    public CL_Response<CL_AnimeGroup_User> SaveGroup(CL_AnimeGroup_Save_Request contract, int userID)
    {
        var contractout = new CL_Response<CL_AnimeGroup_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                contractout.ErrorMessage = "Could not find user with ID: " + userID;
                return contractout;
            }
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            AnimeGroup group;
            var updated = false;
            if (contract.AnimeGroupID is > 0)
            {
                group = RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID.Value);
                if (group is null)
                {
                    contractout.ErrorMessage = "Could not find existing group with ID: " +
                                               contract.AnimeGroupID.Value;
                    return contractout;
                }
            }
            else
            {
                group = new AnimeGroup
                {
                    Description = string.Empty,
                    IsManuallyNamed = 0,
                    DateTimeCreated = DateTime.Now,
                    DateTimeUpdated = DateTime.Now,
                    MissingEpisodeCount = 0,
                    MissingEpisodeCountGroups = 0,
                    OverrideDescription = 0
                };
                updated = true;
            }

            if (string.IsNullOrEmpty(contract.GroupName))
            {
                contractout.ErrorMessage = "Must specify a group name";
                return contractout;
            }

            if (contract.AnimeGroupParentID.HasValue && contract.AnimeGroupParentID.Value > 0)
            {
                // make sure the parent group exists
                var parent = RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupParentID.Value);
                if (parent is null)
                {
                    contractout.ErrorMessage = "Could not find existing parent group with ID: " + contract.AnimeGroupParentID.Value;
                    return contractout;
                }
                if (group.AnimeGroupParentID != group.AnimeGroupID)
                {
                    group.AnimeGroupParentID = parent.AnimeGroupID;
                    updated = true;
                }
            }
            else if (!contract.AnimeGroupParentID.HasValue)
            {
                if (group.AnimeGroupParentID.HasValue)
                {
                    group.AnimeGroupParentID = null;
                    updated = true;
                }
            }

            var mainSeries = group.MainSeries ?? group.AllSeries.FirstOrDefault();
            var customName = !string.IsNullOrEmpty(contract.GroupName) && (group.IsManuallyNamed == 1 || !string.Equals(group.GroupName, contract.GroupName));
            var customDesc = !string.IsNullOrEmpty(contract.Description) && (group.OverrideDescription == 1 || !string.Equals(group.Description, contract.Description));
            if (customName || mainSeries is null)
            {
                if (!string.Equals(group.GroupName, contract.GroupName))
                {
                    group.GroupName = contract.GroupName;
                    updated = true;
                }
                if (group.IsManuallyNamed != 1)
                {
                    group.IsManuallyNamed = 1;
                    updated = true;
                }
            }
            else
            {
                var mainName = mainSeries.Title;
                if (!string.Equals(group.GroupName, mainName))
                {
                    group.GroupName = mainName;
                    updated = true;
                }
                if (group.IsManuallyNamed != 0)
                {
                    group.IsManuallyNamed = 0;
                    updated = true;
                }
            }
            if (customDesc || mainSeries is null)
            {
                if (!string.Equals(group.Description, contract.Description))
                {
                    group.Description = contract.Description;
                    updated = true;
                }
                if (group.OverrideDescription != 1)
                {
                    group.OverrideDescription = 1;
                    updated = true;
                }
            }
            else
            {
                var mainDescription = mainSeries.AniDB_Anime?.Description ?? string.Empty;
                if (!string.Equals(group.Description, mainDescription))
                {
                    group.Description = mainDescription;
                    updated = true;
                }
                if (group.OverrideDescription != 0)
                {
                    group.OverrideDescription = 0;
                    updated = true;
                }
            }

            groupService.ValidateMainSeries(group);
            if (updated)
            {
                group.DateTimeUpdated = DateTime.Now;
                RepoFactory.AnimeGroup.Save(group, true);
            }

            if (mainSeries is not null)
                _userDataService.SetSeriesAsFavorite(mainSeries, user, contract.IsFave == 1);

            contractout.Result = groupService.GetV1Contract(group, userID);

            return contractout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/Move/{animeSeriesID}/{newAnimeGroupID}/{userID}")]
    public CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
    {
        var contractout = new CL_Response<CL_AnimeSeries_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            // make sure the series exists
            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series is null)
            {
                contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID;
                return contractout;
            }

            // make sure the group exists
            var group = newAnimeGroupID <= 0
                ? _groupCreator.GetOrCreateSingleGroupForSeries(series)
                : RepoFactory.AnimeGroup.GetByID(newAnimeGroupID);
            if (group is null)
            {
                contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID;
                return contractout;
            }

            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            seriesService.MoveSeries(series, group);

            contractout.Result = _legacyV1Service.GetV1UserContract(series, userID);

            return contractout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/{userID}")]
    public CL_Response<CL_AnimeSeries_User> SaveSeries(CL_AnimeSeries_Save_Request contract, int userID)
    {
        var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
        var contractout = new CL_Response<CL_AnimeSeries_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            if (contract.AnimeSeriesID.HasValue && contract.AnimeSeriesID.Value > 0)
            {
                var series = RepoFactory.AnimeSeries.GetByID(contract.AnimeSeriesID.Value);
                if (series is null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " + contract.AnimeSeriesID.Value;
                    return contractout;
                }

                if (series.AniDB_ID != contract.AniDB_ID)
                {
                    contractout.ErrorMessage = $"Invalid anime id for series record with ID: {series.AniDB_ID}";
                    return contractout;
                }

                // Check if we are moving a series, and also check the group if we are.
                AnimeGroup group = null;
                var shouldMove = contract.AnimeGroupID != series.AnimeGroupID;
                if (shouldMove)
                {
                    group = contract.AnimeGroupID <= 0
                        ? _groupCreator.GetOrCreateSingleGroupForSeries(series)
                        : RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID);
                    if (group is null)
                    {
                        contractout.ErrorMessage = $"Invalid series group id for series record with ID: {series.AniDB_ID}";
                        return contractout;
                    }
                }
                var updated = shouldMove;

                if (!string.Equals(contract.DefaultAudioLanguage, series.DefaultAudioLanguage))
                {
                    series.DefaultAudioLanguage = contract.DefaultAudioLanguage;
                    updated = true;
                }

                if (!string.Equals(contract.DefaultSubtitleLanguage, series.DefaultSubtitleLanguage))
                {
                    series.DefaultSubtitleLanguage = contract.DefaultSubtitleLanguage;
                    updated = true;
                }

                if (!string.Equals(contract.SeriesNameOverride, series.SeriesNameOverride))
                {
                    series.SeriesNameOverride = contract.SeriesNameOverride;
                    series.ResetPreferredTitle();
                    series.ResetAnimeTitles();
                    updated = true;
                }

                // The move will take care of saving and emitting the event.
                if (shouldMove)
                {
                    seriesService.MoveSeries(series, group);
                }
                else if (updated)
                {
                    series.DateTimeUpdated = DateTime.Now;

                    RepoFactory.AnimeSeries.Save(series, true, true);

                    ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
                }

                contractout.Result = _legacyV1Service.GetV1UserContract(series, userID);
            }
            else
            {
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(contract.AniDB_ID);
                if (anime is null)
                {
                    contractout.ErrorMessage = $"Could not find anime record with ID: {contract.AniDB_ID}";
                    return contractout;
                }

                // Create a new series.
                var series = new AnimeSeries
                {
                    AniDB_ID = anime.AnimeID,
                    LatestLocalEpisodeNumber = 0,
                    DateTimeUpdated = DateTime.Now,
                    DateTimeCreated = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    DefaultAudioLanguage = contract.DefaultAudioLanguage,
                    DefaultSubtitleLanguage = contract.DefaultSubtitleLanguage,
                    SeriesNameOverride = contract.SeriesNameOverride,
                };

                var group = contract.AnimeGroupID <= 0
                    ? _groupCreator.GetOrCreateSingleGroupForAnime(anime)
                    : RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID);
                series.AnimeGroupID = group.AnimeGroupID;

                // Populate before making a group to ensure IDs and stats are set for group filters.
                RepoFactory.AnimeSeries.Save(series, false, false);

                seriesService.CreateAnimeEpisodes(series).ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Added);

                contractout.Result = _legacyV1Service.GetV1UserContract(series, userID);
            }

            return contractout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/CreateFromAnime/{animeID}/{userID}/{animeGroupID?}/{forceOverwrite}")]
    public CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID,
        bool forceOverwrite)
    {
        var response = new CL_Response<CL_AnimeSeries_User> { Result = null, ErrorMessage = string.Empty };
        try
        {
            if (animeGroupID is > 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID.Value);
                if (grp is null)
                {
                    response.ErrorMessage = "Could not find the specified group";
                    return response;
                }
            }

            // make sure a series doesn't already exists for this anime
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (ser != null && !forceOverwrite)
            {
                response.ErrorMessage = "A series already exists for this anime";
                return response;
            }

            // make sure the anime exists first
            var settings = _settingsProvider.GetSettings();
            var refreshMethod = AnidbRefreshMethod.Default | AnidbRefreshMethod.CreateShokoSeries;
            if (settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime)
                refreshMethod |= AnidbRefreshMethod.DownloadRelations;
            var anime = _anidbService.Process(animeID, refreshMethod, 0).Result;
            if (anime is null)
            {
                response.ErrorMessage = "Could not get anime information from AniDB";
                return response;
            }

            ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            // check if we have any group status data for this associated anime
            // if not we will download it now
            if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
            {
                var scheduler = _schedulerFactory.GetScheduler().Result;
                scheduler.StartJob<GetAniDBReleaseGroupStatusJob>(c => c.AnimeID = anime.AnimeID).GetAwaiter().GetResult();
            }

            response.Result = _legacyV1Service.GetV1UserContract(ser, userID);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    [HttpPost("AniDB/Anime/Update/{animeID}")]
    public string UpdateAnimeData(int animeID)
    {
        try
        {
            _anidbService.ScheduleRefreshByID(animeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return string.Empty;
    }

    [HttpPost("AniDB/Anime/GetUpdated/{animeID}")]
    public CL_AniDB_AnimeDetailed GetUpdatedAnimeData(int animeID)
    {
        try
        {
            var user = HttpContext.GetUser();
            var anime = _anidbService.Process(animeID, AnidbRefreshMethod.Remote).GetAwaiter().GetResult();

            // update group status information
            var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
            scheduler.StartJob<GetAniDBReleaseGroupStatusJob>(
                c => (c.AnimeID, c.ForceRefresh) = (animeID, true),
                prioritize: true
            ).GetAwaiter().GetResult();

            return _legacyV1Service.GetV1DetailedContract(anime, user.JMMUserID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpPost("AniDB/Anime/ExternalLinksFlag/{animeID}/{flags}")]
    public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
    {
        _logger.LogTrace("UpdateAnimeDisableExternalLinksFlag is deprecated");
    }

    [HttpPost("Group/DefaultSeries/{animeGroupID}/{animeSeriesID}")]
    public void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return;
            }

            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser is null)
            {
                return;
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.SetMainSeries(grp, ser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpDelete("Group/DefaultSeries/{animeGroupID}")]
    public void RemoveDefaultSeriesForGroup(int animeGroupID)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return;
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.SetMainSeries(grp, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpGet("Group/ForSeries/{animeSeriesID}/{userID}")]
    public CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID)
    {
        try
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            return groupService.GetV1Contract(RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.TopLevelAnimeGroup, userID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpPost("AniDB/Anime/Ignore/{animeID}/{ignoreType}/{userID}")]
    public void IgnoreAnime(int animeID, int ignoreType, int userID)
    {
    }

    [HttpGet("AniDB/Anime/Similar/{animeID}/{userID}")]
    public List<CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
    {
        var links = new List<CL_AniDB_Anime_Similar>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return links;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return links;
            }


            foreach (var link in anime.SimilarAnime)
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                if (animeLink != null)
                {
                    if (!user.AllowedAnime(animeLink))
                    {
                        continue;
                    }
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
                var cl = new CL_AniDB_Anime_Similar
                {
                    AniDB_Anime_SimilarID = link.AniDB_Anime_SimilarID,
                    AnimeID = link.AnimeID,
                    SimilarAnimeID = link.SimilarAnimeID,
                    Approval = link.Approval,
                    Total = link.Total
                };
                cl.AniDB_Anime = _legacyV1Service.GetV1Contract(animeLink);
                cl.AnimeSeries = _legacyV1Service.GetV1UserContract(ser, userID);

                links.Add(cl);
            }

            return links;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return links;
        }
    }

    [HttpGet("AniDB/Anime/Relation/{animeID}/{userID}")]
    public List<CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
    {
        var links = new List<CL_AniDB_Anime_Relation>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return links;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return links;
            }


            foreach (var link in anime.RelatedAnime)
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);
                if (animeLink != null)
                {
                    if (!user.AllowedAnime(animeLink))
                    {
                        continue;
                    }
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);
                var cl = new CL_AniDB_Anime_Relation
                {
                    AniDB_Anime_RelationID = link.AniDB_Anime_RelationID,
                    AnimeID = link.AnimeID,
                    RelationType = link.RelationType,
                    RelatedAnimeID = link.RelatedAnimeID
                };
                cl.AniDB_Anime = _legacyV1Service.GetV1Contract(animeLink);
                cl.AnimeSeries = _legacyV1Service.GetV1UserContract(ser, userID);

                links.Add(cl);
            }

            return links;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return links;
        }
    }

    /// <summary>
    ///     Delete a series, and everything underneath it (episodes, files)
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="deleteFiles">also delete the physical files</param>
    /// <param name="deleteParentGroup"></param>
    /// <returns></returns>
    [HttpDelete("Series/{animeSeriesID}/{deleteFiles}/{deleteParentGroup}")]
    public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser is null)
            {
                return "Series does not exist";
            }

            var animeGroupID = ser.AnimeGroupID;

            foreach (var ep in ser.AllAnimeEpisodes)
            {
                foreach (var vid in ep.VideoLocals)
                {
                    var places = vid.Places;
                    for (var index = 0; index < places.Count; index++)
                    {
                        var place = places[index];
                        try
                        {
                            _videoService.DeleteVideoFile(place, deleteFiles).GetAwaiter().GetResult();
                        }
                        catch (Exception e)
                        {
                            return e.Message;
                        }
                    }
                }

                RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
            }

            RepoFactory.AnimeSeries.Delete(ser.AnimeSeriesID);

            // finally update stats
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp != null)
            {
                if (grp.AllSeries.Count == 0)
                {
                    DeleteAnimeGroup(grp.AnimeGroupID, false);
                }
                else
                {
                    var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
                    groupService.UpdateStatsFromTopLevel(grp.TopLevelAnimeGroup, true, true);
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("AniDB/Anime/{animeID}")]
    public CL_AniDB_Anime GetAnime(int animeID)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            return _legacyV1Service.GetV1Contract(anime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpGet("AniDB/Anime")]
    public List<CL_AniDB_Anime> GetAllAnime()
    {
        try
        {
            return RepoFactory.AniDB_Anime.GetAll().Select(_legacyV1Service.GetV1Contract).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_AniDB_Anime>();
    }

    [HttpGet("AniDB/Anime/Rating/{collectionState}/{watchedState}/{ratingVotedState}/{userID}")]
    public List<CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState, int userID)
    {
        var contracts = new List<CL_AnimeRating>();

        try
        {
            var allSeries = RepoFactory.AnimeSeries.GetAll();
            var dictSeries = new Dictionary<int, AnimeSeries>();
            foreach (var ser in allSeries)
            {
                dictSeries[ser.AniDB_ID] = ser;
            }
            var animes = RepoFactory.AniDB_Anime.GetAll();

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return contracts;
            }

            var i = 0;

            foreach (var anime in animes)
            {
                i++;

                // evaluate collection states
                if (collectionState == 2 /* AllEpisodesInMyCollection */)
                {
                    if (!anime.GetFinishedAiring())
                    {
                        continue;
                    }

                    if (!dictSeries.TryGetValue(anime.AnimeID, out var series))
                    {
                        continue;
                    }

                    if (series.MissingEpisodeCount > 0)
                    {
                        continue;
                    }
                }

                if (collectionState == 1 /* InMyCollection */)
                {
                    if (!dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }
                }

                if (collectionState == 3 /* NotInMyCollection */)
                {
                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }
                }

                if (!user.AllowedAnime(anime))
                {
                    continue;
                }

                // evaluate watched states
                if (watchedState == 1 /* AllEpisodesWatched */)
                {
                    if (!dictSeries.TryGetValue(anime.AnimeID, out var series))
                    {
                        continue;
                    }

                    var userRec = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
                    if (userRec is null)
                    {
                        continue;
                    }

                    if (userRec.UnwatchedEpisodeCount > 0)
                    {
                        continue;
                    }
                }

                if (watchedState == 2 /* NotWatched */)
                {
                    if (dictSeries.TryGetValue(anime.AnimeID, out var series))
                    {
                        var userRec = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
                        if (userRec != null)
                        {
                            if (userRec.UnwatchedEpisodeCount == 0)
                            {
                                continue;
                            }
                        }
                    }
                }

                // evaluate voted states
                if (ratingVotedState == 1 /* Voted */)
                {
                    if (!dictSeries.TryGetValue(anime.AnimeID, out var series))
                    {
                        continue;
                    }

                    var seriesUserData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
                    if (seriesUserData is not { HasUserRating: true })
                    {
                        continue;
                    }
                }

                if (ratingVotedState == 2 /* NotVoted */)
                {
                    if (dictSeries.TryGetValue(anime.AnimeID, out var series))
                    {
                        var seriesUserData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
                        if (seriesUserData is { HasUserRating: true })
                        {
                            continue;
                        }
                    }
                }

                var contract = new CL_AnimeRating { AnimeID = anime.AnimeID, AnimeDetailed = _legacyV1Service.GetV1DetailedContract(anime, user.JMMUserID) };
                if (dictSeries.TryGetValue(anime.AnimeID, out var series1))
                {
                    contract.AnimeSeries = _legacyV1Service.GetV1UserContract(series1, userID);
                }

                contracts.Add(contract);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return contracts;
    }

    [HttpGet("AniDB/Anime/Detailed")]
    public List<CL_AniDB_AnimeDetailed> GetAllAnimeDetailed()
    {
        try
        {
            return RepoFactory.AniDB_Anime.GetAll().Select(_legacyV1Service.GetV1DetailedContract).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_AniDB_AnimeDetailed>();
    }

    [HttpGet("Series/{userID}")]
    public List<CL_AnimeSeries_User> GetAllSeries(int userID)
    {
        try
        {
            return RepoFactory.AnimeSeries.GetAll().Select(a => _legacyV1Service.GetV1UserContract(a, userID)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_AnimeSeries_User>();
    }

    [HttpGet("AniDB/Anime/Detailed/{animeID}")]
    public CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
    {
        try
        {
            var user = HttpContext.GetUser();
            return _legacyV1Service.GetV1DetailedContract(RepoFactory.AniDB_Anime.GetByAnimeID(animeID), user.JMMUserID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpGet("Group/SubGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID)
    {
        var retGroups = new List<CL_AnimeGroup_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return retGroups;
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            foreach (var grpChild in grp.Children)
            {
                var ugrp = groupService.GetV1Contract(grpChild, userID);
                if (ugrp != null) retGroups.Add(ugrp);
            }

            return retGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return retGroups;
    }

    [HttpGet("Series/ForGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID)
    {
        var series = new List<CL_AnimeSeries_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return series;
            }

            foreach (var ser in grp.Series)
            {
                var s = _legacyV1Service.GetV1UserContract(ser, userID);
                if (s != null)
                {
                    series.Add(s);
                }
            }

            return series;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return series;
        }
    }

    [HttpGet("Series/ForGroupRecursive/{animeGroupID}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID)
    {
        var series = new List<CL_AnimeSeries_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp is null)
            {
                return series;
            }

            foreach (var ser in grp.AllSeries)
            {
                var s = _legacyV1Service.GetV1UserContract(ser, userID);
                if (s != null)
                {
                    series.Add(s);
                }
            }

            return series;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return series;
        }
    }

    #endregion

    #region Group Filters

    [HttpPost("GroupFilter")]
    public CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract)
    {
        var response = new CL_Response<CL_GroupFilter> { ErrorMessage = string.Empty, Result = null };

        // Process the group
        FilterPreset gf = null;
        if (contract.GroupFilterID != 0)
        {
            gf = RepoFactory.FilterPreset.GetByID(contract.GroupFilterID);
            if (gf is null)
            {
                response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                        contract.GroupFilterID;
                return response;
            }
        }

        var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
        var newFilter = legacyConverter.FromClient(contract);
        if (gf is null)
        {
            gf = newFilter;
        }
        else
        {
            gf.Name = newFilter.Name;
            gf.Hidden = newFilter.Hidden;
            gf.ApplyAtSeriesLevel = newFilter.ApplyAtSeriesLevel;
            gf.Expression = newFilter.Expression;
            gf.SortingExpression = newFilter.SortingExpression;
        }

        RepoFactory.FilterPreset.Save(gf);

        response.Result = legacyConverter.ToClient(gf);
        return response;
    }

    [HttpDelete("GroupFilter/{groupFilterID}")]
    public string DeleteGroupFilter(int groupFilterID)
    {
        try
        {
            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);
            if (gf is null) return "Group Filter not found";

            RepoFactory.FilterPreset.Delete(groupFilterID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("GroupFilter/Detailed/{groupFilterID}/{userID}")]
    public CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
    {
        try
        {
            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);
            if (gf is null)
            {
                return null;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return null;
            }

            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            var model = legacyConverter.ToClient(gf);
            return new CL_GroupFilterExtended
            {
                GroupFilter = model,
                GroupCount = model.Groups[userID].Count,
                SeriesCount = model.Series[userID].Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpGet("GroupFilter/Detailed/ForUser/{userID}")]
    public List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
    {
        var gfs = new List<CL_GroupFilterExtended>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return gfs;
            }

            var allGfs = RepoFactory.FilterPreset.GetAll();
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs).Select(a => new CL_GroupFilterExtended
            {
                GroupFilter = a,
                GroupCount = a.Groups[userID].Count,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return gfs;
    }

    [HttpGet("GroupFilter/Detailed/ForUser/{userID}/{gfparentid}")]
    public List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0)
    {
        var gfs = new List<CL_GroupFilterExtended>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return gfs;
            }

            var allGfs = gfparentid == 0
                ? RepoFactory.FilterPreset.GetTopLevel()
                : RepoFactory.FilterPreset.GetByParentID(gfparentid);
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs).Select(a => new CL_GroupFilterExtended
            {
                GroupFilter = a,
                GroupCount = a.Groups.FirstOrDefault().Value.Count,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return gfs;
    }

    [HttpGet("GroupFilter")]
    public List<CL_GroupFilter> GetAllGroupFilters()
    {
        var gfs = new List<CL_GroupFilter>();
        try
        {
            var start = DateTime.Now;

            var allGfs = RepoFactory.FilterPreset.GetAll();
            var ts = DateTime.Now - start;
            _logger.LogInformation("GetAllGroupFilters (Database) in {Milliseconds}ms", ts.TotalMilliseconds);

            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs)
                .WhereNotNull()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return gfs;
    }

    [HttpGet("GroupFilter/Parent/{gfparentid}")]
    public List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0)
    {
        var gfs = new List<CL_GroupFilter>();
        try
        {
            var start = DateTime.Now;

            var allGfs = gfparentid == 0 ? RepoFactory.FilterPreset.GetTopLevel() : RepoFactory.FilterPreset.GetByParentID(gfparentid);
            var ts = DateTime.Now - start;
            _logger.LogInformation("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs)
                .WhereNotNull()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return gfs;
    }

    [HttpGet("GroupFilter/{gf}")]
    public CL_GroupFilter GetGroupFilter(int gf)
    {
        try
        {
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            return legacyConverter.ToClient(RepoFactory.FilterPreset.GetByID(gf));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    [HttpPost("GroupFilter/Evaluate")]
    public CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract)
    {
        try
        {
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            var filter = legacyConverter.FromClient(contract);
            var model = legacyConverter.ToClient(filter);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return new CL_GroupFilter();
        }
    }

    #endregion

    #region Playlists

    [HttpGet("Playlist")]
    public List<Playlist> GetAllPlaylists()
    {
        try
        {
            return RepoFactory.Playlist.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<Playlist>();
    }

    [HttpPost("Playlist")]
    public CL_Response<Playlist> SavePlaylist(Playlist contract)
    {
        var contractRet = new CL_Response<Playlist> { ErrorMessage = string.Empty };
        try
        {
            // Process the playlist
            Playlist pl = null;
            if (contract.PlaylistID != 0)
            {
                pl = RepoFactory.Playlist.GetByID(contract.PlaylistID);
                if (pl is null)
                {
                    contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                               contract.PlaylistID;
                    return contractRet;
                }
            }
            else
            {
                pl = new Playlist();
            }

            if (string.IsNullOrEmpty(contract.PlaylistName))
            {
                contractRet.ErrorMessage = "Playlist must have a name";
                return contractRet;
            }

            pl.DefaultPlayOrder = contract.DefaultPlayOrder;
            pl.PlaylistItems = contract.PlaylistItems;
            pl.PlaylistName = contract.PlaylistName;
            pl.PlayUnwatched = contract.PlayUnwatched;
            pl.PlayWatched = contract.PlayWatched;

            RepoFactory.Playlist.Save(pl);

            contractRet.Result = pl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("Playlist/{playlistID}")]
    public string DeletePlaylist(int playlistID)
    {
        try
        {
            var pl = RepoFactory.Playlist.GetByID(playlistID);
            if (pl is null)
            {
                return "Playlist not found";
            }

            RepoFactory.Playlist.Delete(playlistID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("Playlist/{playlistID}")]
    public Playlist GetPlaylist(int playlistID)
    {
        try
        {
            return RepoFactory.Playlist.GetByID(playlistID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    #endregion

    #region Custom Tags

    [HttpGet("CustomTag")]
    public List<CustomTag> GetAllCustomTags()
    {
        try
        {
            return RepoFactory.CustomTag.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return new List<CustomTag>();
        }
    }

    [HttpPost("CustomTag/CrossRef")]
    public CL_Response<CL_CrossRef_CustomTag> SaveCustomTagCrossRef(CL_CrossRef_CustomTag contract)
    {
        var contractRet = new CL_Response<CL_CrossRef_CustomTag> { ErrorMessage = string.Empty };
        try
        {
            if (contract.CrossRefType is not 1 /* Anime */)
            {
                contractRet.ErrorMessage = "CrossRefType must be 1 (Anime)";
                return contractRet;
            }

            // this is an update
            CrossRef_CustomTag xref = null;
            if (contract.CrossRef_CustomTagID != 0)
            {
                contractRet.ErrorMessage = "Updates are not allowed";
                return contractRet;
            }

            xref = new CrossRef_CustomTag
            {
                CrossRefID = contract.CrossRefID,
                CustomTagID = contract.CustomTagID
            };


            RepoFactory.CrossRef_CustomTag.Save(xref);

            contractRet.Result = xref.ToClient();
            var jobFactory = Utils.ServiceContainer.GetRequiredService<JobFactory>();
            jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = contract.CrossRefID).Process().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("CustomTag/CrossRef/{xrefID}")]
    public string DeleteCustomTagCrossRefByID(int xrefID)
    {
        try
        {
            var pl = RepoFactory.CrossRef_CustomTag.GetByID(xrefID);
            if (pl is null)
            {
                return "Custom Tag not found";
            }

            RepoFactory.CrossRef_CustomTag.Delete(xrefID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpDelete("CustomTag/CrossRef/{customTagID}/{crossRefType}/{crossRefID}")]
    public string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID)
    {
        try
        {
            if (crossRefType is not 1 /* CustomTagCrossRefType.Anime */)
                return "Custom Tag not found";

            var xref = RepoFactory.CrossRef_CustomTag.GetByUniqueID(customTagID, crossRefID);
            if (xref is null)
                return "Custom Tag not found";

            RepoFactory.CrossRef_CustomTag.Delete(xref.CrossRef_CustomTagID);
            var jobFactory = Utils.ServiceContainer.GetRequiredService<JobFactory>();
            jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = crossRefID).Process().GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("CustomTag")]
    public CL_Response<CustomTag> SaveCustomTag(CustomTag contract)
    {
        var contractRet = new CL_Response<CustomTag> { ErrorMessage = string.Empty };
        try
        {
            // this is an update
            CustomTag ctag = null;
            if (contract.CustomTagID != 0)
            {
                ctag = RepoFactory.CustomTag.GetByID(contract.CustomTagID);
                if (ctag is null)
                {
                    contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                               contract.CustomTagID;
                    return contractRet;
                }
            }
            else
            {
                ctag = new CustomTag();
            }

            if (string.IsNullOrEmpty(contract.TagName))
            {
                contractRet.ErrorMessage = "Custom Tag must have a name";
                return contractRet;
            }

            ctag.TagName = contract.TagName;
            ctag.TagDescription = contract.TagDescription;

            RepoFactory.CustomTag.Save(ctag);

            contractRet.Result = ctag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("CustomTag/{customTagID}")]
    public string DeleteCustomTag(int customTagID)
    {
        try
        {
            var pl = RepoFactory.CustomTag.GetByID(customTagID);
            if (pl is null)
            {
                return "Custom Tag not found";
            }

            // first get a list of all the anime that referenced this tag
            var xrefs = RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTagID);

            RepoFactory.CustomTag.Delete(customTagID);

            // update cached data for any anime that were affected
            var jobFactory = Utils.ServiceContainer.GetRequiredService<JobFactory>();
            Task.WhenAll(xrefs.Select(xref => jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = xref.CrossRefID).Process())).GetAwaiter().GetResult();

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("CustomTag/{customTagID}")]
    public CustomTag GetCustomTag(int customTagID)
    {
        try
        {
            return RepoFactory.CustomTag.GetByID(customTagID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    #endregion

    #region Users

    [HttpGet("User")]
    public List<CL_JMMUser> GetAllUsers()
    {
        try
        {
            return RepoFactory.JMMUser.GetAll()
                .Select(u => new CL_JMMUser
                {
                    JMMUserID = u.JMMUserID,
                    Username = u.Username,
                    IsAniDBUser = u.IsAniDBUser,
                    IsTraktUser = u.IsTraktUser,
                    CanEditServerSettings = u.CanEditServerSettings,
                    HideCategories = u.HideCategories,
                    IsAdmin = u.IsAdmin,
                    Password = "**SECRET**",
                    PlexToken = "**SECRET**",
                    PlexUsers = u.PlexUsers,
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return [];
        }
    }

    [HttpPost("User/{username}")]
    public CL_JMMUser AuthenticateUser(string username, [FromForm] string password)
    {
        try
        {
            var service = Utils.ServiceContainer.GetRequiredService<IUserService>();
            username = username.Replace("+", " ");
            if (service.AuthenticateUser(username, password) is not JMMUser user)
                return null;

            return new()
            {
                JMMUserID = user.JMMUserID,
                Username = user.Username,
                IsAniDBUser = user.IsAniDBUser,
                IsTraktUser = user.IsTraktUser,
                CanEditServerSettings = user.CanEditServerSettings,
                HideCategories = user.HideCategories,
                IsAdmin = user.IsAdmin,
                Password = "**SECRET**",
                PlexToken = "**SECRET**",
                PlexUsers = user.PlexUsers,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return null;
        }
    }

    [HttpPost("User/ChangePassword/{userID}")]
    public string ChangePassword(int userID, [FromForm] string newPassword)
    {
        return ChangePassword(userID, newPassword, true);
    }

    [NonAction]
    public string ChangePassword(int userID, string newPassword, bool revokeapikey)
    {
        try
        {
            var service = Utils.ServiceContainer.GetRequiredService<IUserService>();
            var user = service.GetUserByID(userID);
            if (user is null)
                return "User not found";

            service.ChangeUserPassword(user, newPassword)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            if (revokeapikey)
                service.InvalidateRestApiTokensForUser(user)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("User")]
    public string SaveUser(CL_JMMUser user)
    {
        try
        {
            var service = Utils.ServiceContainer.GetRequiredService<IUserService>();
            JMMUser jmmUser = null;
            var tags = user.HideCategories?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(RepoFactory.AniDB_Tag.GetByName)
                .WhereNotNull()
                .OrderBy(tag => tag.TagID)
                .Cast<IAnidbTag>()
                .ToList();
            if (user.JMMUserID != 0)
            {
                jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                if (jmmUser is null)
                {
                    return "User not found";
                }
                service.UpdateUser(jmmUser, new()
                {
                    Username = user.Username,
                    Password = user.Password is not "**SECRET**" ? user.Password : null,
                    IsAdmin = user.IsAdmin == 1,
                    IsAnidbUser = user.IsAniDBUser == 1,
                    RestrictedTags = tags,
                });
            }
            else
            {
                jmmUser = (JMMUser)service.CreateUser(new()
                {
                    Username = user.Username,
                    Password = user.Password is not "**SECRET**" ? user.Password : string.Empty,
                    IsAdmin = user.IsAdmin == 1,
                    IsAnidbUser = user.IsAniDBUser == 1,
                    RestrictedTags = tags,
                })
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }

            // Extra handling for things not exposed to the plugin API and
            // probably never will.
            if (
                !string.Equals(jmmUser.PlexUsers, user.PlexUsers, StringComparison.InvariantCultureIgnoreCase) ||
                !string.Equals(jmmUser.PlexToken, user.PlexToken, StringComparison.InvariantCultureIgnoreCase) ||
                jmmUser.IsTraktUser != user.IsTraktUser
            )
            {
                jmmUser.IsTraktUser = user.IsTraktUser;
                jmmUser.PlexUsers = user.PlexUsers;
                if (user.PlexToken is not "**SECRET**")
                {
                    jmmUser.PlexToken = user.PlexToken;
                }

                RepoFactory.JMMUser.Save(jmmUser);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpDelete("User")]
    public string DeleteUser(int userID)
    {
        try
        {
            var jmmUser = RepoFactory.JMMUser.GetByID(userID);
            if (jmmUser is null)
                return "User not found";

            Utils.ServiceContainer.GetRequiredService<IUserService>().DeleteUser(jmmUser)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }

        return string.Empty;
    }

    #endregion

    #region Import Folders

    [HttpGet("Folder")]
    public List<CL_ImportFolder> GetImportFolders()
    {
        try
        {
            return RepoFactory.ShokoManagedFolder.GetAll().Select(a => a.ToClient()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return new List<CL_ImportFolder>();
    }

    [HttpPost("Folder")]
    public CL_Response<CL_ImportFolder> SaveImportFolder(CL_ImportFolder contract)
    {
        var folder = new CL_Response<CL_ImportFolder>();
        try
        {
            folder.Result = RepoFactory.ShokoManagedFolder.SaveFolder(contract.ToServer()).ToClient();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Ex}", e);
            folder.ErrorMessage = e.Message;
        }

        return folder;
    }

    [HttpDelete("Folder/{importFolderID}")]
    public string DeleteImportFolder(int importFolderID)
    {
        var importFolder = RepoFactory.ShokoManagedFolder.GetByID(importFolderID);
        if (importFolder == null)
            return "ImportFolder not found";
        _videoService.RemoveManagedFolder(importFolder);
        return string.Empty;
    }

    #endregion
}
