using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class DashboardController : BaseController
{
    private readonly SeriesFactory _seriesFactory;
    private readonly QueueHandler _queueHandler;
    private readonly AnimeSeriesService _seriesService;
    private readonly AnimeSeries_UserRepository _seriesUser;
    private readonly VideoLocal_UserRepository _vlUsers;

    /// <summary>
    /// Get the counters of various collection stats
    /// </summary>
    /// <returns></returns>
    [HttpGet("Stats")]
    public Dashboard.CollectionStats GetStats()
    {
        var allSeries = RepoFactory.AnimeSeries.GetAll()
            .Where(a => User.AllowedSeries(a))
            .ToList();
        var groupCount = allSeries
            .DistinctBy(a => a.AnimeGroupID)
            .Count();
        var episodeDict = allSeries
            .ToDictionary(s => s, s => s.AllAnimeEpisodes);
        var episodes = episodeDict.Values
            .SelectMany(episodeList => episodeList)
            .ToList();
        var files = episodes
            .SelectMany(a => a.VideoLocals)
            .DistinctBy(a => a.VideoLocalID)
            .ToList();
        var totalFileSize = files
            .Sum(a => a.FileSize);
        var watchedEpisodes = episodes
            .Where(a => a.GetUserRecord(User.JMMUserID)?.WatchedDate != null)
            .ToList();
        // Count local watched series in the user's collection.
        var watchedSeries = allSeries.Count(series =>
        {
            // If we don't have an anime entry then something is very wrong, but
            // we don't care about that right now, so just skip it.
            var anime = series.AniDB_Anime;
            if (anime == null)
                return false;

            // If the series doesn't have any episodes, then skip it.
            if (anime.EpisodeCountNormal == 0)
                return false;

            // If all the normal episodes are still missing, then skip it.
            var missingNormalEpisodesTotal = series.MissingEpisodeCount + series.HiddenMissingEpisodeCount;
            if (anime.EpisodeCountNormal == missingNormalEpisodesTotal)
                return false;

            // If we don't have a user record for the series, then skip it.
            var record = _seriesUser.GetByUserAndSeriesID(User.JMMUserID, series.AnimeSeriesID);
            if (record == null)
                return false;

            // Check if we've watched more or equal to the number of watchable
            // normal episodes.
            var totalWatchableNormalEpisodes = anime.EpisodeCountNormal - missingNormalEpisodesTotal;
            var count = episodeDict[series]
                .Count(episode => episode.AniDB_Episode.GetEpisodeTypeEnum() == EpisodeType.Episode &&
                                  episode.GetUserRecord(User.JMMUserID)?.WatchedDate != null);
            return count >= totalWatchableNormalEpisodes;
        });
        // Calculate watched hours for both local episodes and non-local episodes.
        var hoursWatched = Math.Round(
            (decimal)watchedEpisodes.Sum(a => a.VideoLocals.FirstOrDefault()?.DurationTimeSpan.TotalHours ?? new TimeSpan(0, 0, a.AniDB_Episode?.LengthSeconds ?? 0).TotalHours),
            1, MidpointRounding.AwayFromZero);
        var places = files
            // We cache the video local here since it may be gone later if the files are actively being removed.
            .SelectMany(a => a.Places.Select(b => new { VideoLocalID = a.VideoLocalID, VideoLocal = a, Place = b }))
            .ToList();
        var duplicates = places
            .Where(a => !a.VideoLocal.IsVariation)
            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.VideoLocal.Hash))
            .GroupBy(a => a.EpisodeID)
            .Count(a => a.Count() > 1);
        var percentDuplicates = places.Count == 0
            ? 0
            : Math.Round((decimal)duplicates * 100 / places.Count, 2, MidpointRounding.AwayFromZero);
        var missingEpisodes = allSeries.Sum(a => a.MissingEpisodeCount);
        var missingEpisodesCollecting = allSeries.Sum(a => a.MissingEpisodeCountGroups);
        var multipleEpisodes = episodes.Count(a => a.VideoLocals.Count(b => !b.IsVariation) > 1);
        var unrecognizedFiles = RepoFactory.VideoLocal.GetVideosWithoutEpisodeUnsorted().Count;
        var duplicateFiles = places.GroupBy(a => a.VideoLocalID).Count(a => a.Count() > 1);
        var seriesWithMissingLinks = allSeries.Count(MissingBothTvDBAndMovieDBLink);
        return new()
        {
            FileCount = files.Count,
            FileSize = totalFileSize,
            SeriesCount = allSeries.Count,
            GroupCount = groupCount,
            FinishedSeries = watchedSeries,
            WatchedEpisodes = watchedEpisodes.Count,
            WatchedHours = hoursWatched,
            PercentDuplicate = percentDuplicates,
            MissingEpisodes = missingEpisodes,
            MissingEpisodesCollecting = missingEpisodesCollecting,
            UnrecognizedFiles = unrecognizedFiles,
            SeriesWithMissingLinks = seriesWithMissingLinks,
            EpisodesWithMultipleFiles = multipleEpisodes,
            FilesWithDuplicateLocations = duplicateFiles
        };
    }

    private static bool MissingBothTvDBAndMovieDBLink(SVR_AnimeSeries ser)
    {
        if (ser.AniDB_Anime.Restricted > 0)
        {
            return false;
        }

        // this is fast now
        var movieLinkMissing = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(ser.AniDB_ID, CrossRefType.MovieDB) == null;
        var tvlinkMissing = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID).Count == 0;
        return movieLinkMissing && tvlinkMissing;
    }

    /// <summary>
    /// Gets the top <para>number</para> of the most common tags visible to the current user.
    /// </summary>
    /// <param name="number">The max number of results to return. (Defaults to 10)</param>
    /// <param name="filter">The <see cref="TagFilter.Filter" /> to use. (Defaults to <see cref="TagFilter.Filter.AnidbInternal" /> | <see cref="TagFilter.Filter.Misc" /> | <see cref="TagFilter.Filter.Source" />)</param>
    /// <returns></returns>
    [HttpGet("TopTags/{number}")]
    [Obsolete]
    public List<Tag> GetTopTagsObsolete(int number = 10,
        [FromQuery] TagFilter.Filter filter =
            TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source)
        => GetTopTags(number, 1, filter);

    /// <summary>
    /// Gets the top number of the most common tags visible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="filter">The <see cref="TagFilter.Filter" /> to use. (Defaults to <see cref="TagFilter.Filter.AnidbInternal" /> | <see cref="TagFilter.Filter.Misc" /> | <see cref="TagFilter.Filter.Source" />)</param>
    /// <returns></returns>
    [HttpGet("TopTags")]
    public List<Tag> GetTopTags([FromQuery] [Range(0, 100)]  int pageSize = 10, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] TagFilter.Filter filter =
            TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source)
    {
        var tags = RepoFactory.AniDB_Anime_Tag.GetAllForLocalSeries()
            .GroupBy(xref => xref.TagID)
            .Select(xrefList => (tag: RepoFactory.AniDB_Tag.GetByTagID(xrefList.Key), weight: xrefList.Count()))
            .Where(tuple => tuple.tag != null && User.AllowedTag(tuple.tag))
            .OrderByDescending(tuple => tuple.weight)
            .Select(tuple => new Tag(tuple.tag, true) { Weight = tuple.weight })
            .ToList();
        var tagDict = tags
            .ToDictionary(tag => tag.Name.ToLowerInvariant());
        var tagFilter = new TagFilter<Tag>(name => tagDict.TryGetValue(name.ToLowerInvariant(), out var tag)
            ? tag : null, tag => tag.Name, name => new Tag { Name = name, Weight = 0 });
        if (pageSize <= 0)
            return tagFilter
                .ProcessTags(filter, tags)
                .ToList();
        return tagFilter
            .ProcessTags(filter, tags)
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Gets counts for all of the commands currently queued
    /// </summary>
    /// <returns></returns>
    [HttpGet("QueueSummary")]
    [Obsolete("Use /api/v3/Queue/Types instead.")]
    public async Task<Dictionary<string, int>> GetQueueSummary()
    {
        return await _queueHandler.GetJobCounts();
    }

    /// <summary>
    /// Gets a breakdown of which types of anime the user has access to
    /// </summary>
    /// <returns></returns>
    [HttpGet("SeriesSummary")]
    public Dashboard.SeriesSummary GetSeriesSummary()
    {
        var series = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a))
            .GroupBy(a => (AnimeType)(a.AniDB_Anime?.AnimeType ?? -1))
            .ToDictionary(a => a.Key, a => a.Count());

        return new Dashboard.SeriesSummary
        {
            Series = series.GetValueOrDefault(AnimeType.TVSeries, 0),
            Special = series.GetValueOrDefault(AnimeType.TVSpecial, 0),
            Movie = series.GetValueOrDefault(AnimeType.Movie, 0),
            OVA = series.GetValueOrDefault(AnimeType.OVA, 0),
            Web = series.GetValueOrDefault(AnimeType.Web, 0),
            Other = series.GetValueOrDefault(AnimeType.Other, 0),
            None = series.GetValueOrDefault(AnimeType.None, 0)
        };
    }

    /// <summary>
    /// Get a list of recently added <see cref="Dashboard.EpisodeDetails"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedEpisodes")]
    public List<Dashboard.EpisodeDetails> GetRecentlyAddedEpisodes([FromQuery] [Range(0, 100)] int pageSize = 30,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeRestricted = false)
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.VideoLocal.GetAll()
            .Where(f => f.DateTimeImported.HasValue)
            .OrderByDescending(f => f.DateTimeImported)
            .SelectMany(file => file.AnimeEpisodes.Select(episode => (file, episode)));
        var seriesDict = episodeList
            .DistinctBy(tuple => tuple.episode.AnimeSeriesID)
            .Select(tuple => tuple.episode.AnimeSeries)
            .Where(series => series != null && user.AllowedSeries(series))
            .ToDictionary(series => series.AnimeSeriesID);
        var animeDict = seriesDict.Values
            .ToDictionary(series => series.AnimeSeriesID, series => series.AniDB_Anime);

        if (pageSize <= 0)
        {
            return episodeList
                .Where(tuple => animeDict.TryGetValue(tuple.episode.AnimeSeriesID, out var anime) &&
                        (includeRestricted || anime.Restricted == 0))
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode,
                    seriesDict[tuple.episode.AnimeSeriesID], animeDict[tuple.episode.AnimeSeriesID], tuple.file))
                .ToList();
        }

        return episodeList
            .Where(tuple => animeDict.TryGetValue(tuple.episode.AnimeSeriesID, out var anime) &&
                    (includeRestricted || anime.Restricted == 0))
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode,
                seriesDict[tuple.episode.AnimeSeriesID], animeDict[tuple.episode.AnimeSeriesID], tuple.file))
            .ToList();
    }

    /// <summary>
    /// Get a list of recently added <see cref="Series"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedSeries")]
    public List<Series> GetRecentlyAddedSeries([FromQuery] [Range(0, 100)] int pageSize = 20,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeRestricted = false)
    {
        var user = HttpContext.GetUser();
        var seriesList = RepoFactory.VideoLocal.GetAll()
            .Where(f => f.DateTimeImported.HasValue)
            .OrderByDescending(f => f.DateTimeImported)
            .SelectMany(file => file.AnimeEpisodes.Select(episode => episode.AnimeSeriesID))
            .Distinct()
            .Select(seriesID => RepoFactory.AnimeSeries.GetByID(seriesID))
            .Where(series => series != null && user.AllowedSeries(series) &&
                (includeRestricted || series.AniDB_Anime.Restricted != 1));

        if (pageSize <= 0)
        {
            return seriesList
                .Select(a => _seriesFactory.GetSeries(a))
                .ToList();
        }

        return seriesList
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .Select(a => _seriesFactory.GetSeries(a))
            .ToList();
    }

    /// <summary>
    /// Get a list of the episodes to continue watching (soon-to-be) in recently watched order.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("ContinueWatchingEpisodes")]
    public List<Dashboard.EpisodeDetails> GetContinueWatchingEpisodes([FromQuery] [Range(0, 100)] int pageSize = 20,
        [FromQuery] [Range(0, int.MaxValue)] int page = 0, [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeRestricted = false)
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.AnimeSeries_User.GetByUserID(user.JMMUserID)
            .Where(record => record.LastEpisodeUpdate.HasValue)
            .OrderByDescending(record => record.LastEpisodeUpdate)
            .Select(record => RepoFactory.AnimeSeries.GetByID(record.AnimeSeriesID))
            .Where(series => user.AllowedSeries(series) &&
                (includeRestricted || series.AniDB_Anime.Restricted != 1))
            .Select(series => (series, episode: _seriesService.GetActiveEpisode(series, user.JMMUserID, includeSpecials)))
            .Where(tuple => tuple.episode != null);
        if (pageSize <= 0)
        {
            return episodeList
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series))
                .ToList();
        }

        return episodeList
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series))
            .ToList();
    }

    /// <summary>
    /// Get the next episodes for series that currently don't have an active watch session for the user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="onlyUnwatched">Only show unwatched episodes.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <returns></returns>
    [HttpGet("NextUpEpisodes")]
    public List<Dashboard.EpisodeDetails> GetNextUpEpisodes([FromQuery] [Range(0, 100)] int pageSize = 20,
        [FromQuery] [Range(0, int.MaxValue)] int page = 0, [FromQuery] bool onlyUnwatched = true,
        [FromQuery] bool includeSpecials = true, [FromQuery] bool includeRestricted = false,
        [FromQuery] bool includeMissing = false, [FromQuery] bool includeHidden = false,
        [FromQuery] bool includeRewatching = false)
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.AnimeSeries_User.GetByUserID(user.JMMUserID)
            .Where(record =>
                record.LastEpisodeUpdate.HasValue && (onlyUnwatched ? record.UnwatchedEpisodeCount > 0 : true))
            .OrderByDescending(record => record.LastEpisodeUpdate)
            .Select(record => RepoFactory.AnimeSeries.GetByID(record.AnimeSeriesID))
            .Where(series => user.AllowedSeries(series) &&
                (includeRestricted || series.AniDB_Anime.Restricted != 1))
            .Select(series => (series, episode: _seriesService.GetNextEpisode(series, user.JMMUserID, new()
                {
                    DisableFirstEpisode = true,
                    IncludeCurrentlyWatching = !onlyUnwatched,
                    IncludeHidden = includeHidden,
                    IncludeMissing = includeMissing,
                    IncludeRewatching = includeRewatching,
                    IncludeSpecials = includeSpecials,
                })))
            .Where(tuple => tuple.episode != null);
        if (pageSize <= 0)
        {
            return episodeList
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series))
                .ToList();
        }

        return episodeList
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series))
            .ToList();
    }

    [NonAction]
    public Dashboard.EpisodeDetails GetEpisodeDetailsForSeriesAndEpisode(SVR_JMMUser user, SVR_AnimeEpisode episode,
        SVR_AnimeSeries series, SVR_AniDB_Anime anime = null, SVR_VideoLocal file = null)
    {
        SVR_VideoLocal_User userRecord;
        var animeEpisode = episode.AniDB_Episode;
        anime ??= series.AniDB_Anime;

        if (file != null)
        {
            userRecord = _vlUsers.GetByUserIDAndVideoLocalID(user.JMMUserID, file.VideoLocalID);
        }
        else
        {
            (file, userRecord) = episode.VideoLocals
                .Select(f => (file: f, userRecord: _vlUsers.GetByUserIDAndVideoLocalID(user.JMMUserID, f.VideoLocalID)))
                .OrderByDescending(tuple => tuple.userRecord?.LastUpdated)
                .ThenByDescending(tuple => tuple.file.DateTimeCreated)
                .FirstOrDefault();
        }

        return new Dashboard.EpisodeDetails(animeEpisode, anime, series, file, userRecord);
    }

    /// <summary>
    /// Get the next <paramref name="numberOfDays"/> from the AniDB Calendar.
    /// </summary>
    /// <param name="numberOfDays">Number of days to show.</param>
    /// <param name="showAll">Show all series.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("AniDBCalendar")]
    public List<Dashboard.EpisodeDetails> GetAniDBCalendarInDays([FromQuery] int numberOfDays = 7,
        [FromQuery] bool showAll = false, [FromQuery] bool includeRestricted = false)
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.AniDB_Episode.GetForDate(DateTime.Today, DateTime.Today.AddDays(numberOfDays))
            .ToList();
        var animeDict = episodeList
            .Select(episode => RepoFactory.AniDB_Anime.GetByAnimeID(episode.AnimeID))
            .Distinct()
            .ToDictionary(anime => anime.AnimeID);
        var seriesDict = animeDict.Values
            .Select(anime => RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID))
            .Where(series => series != null)
            .Distinct()
            .ToDictionary(anime => anime.AniDB_ID);
        return episodeList
            .Where(episode => animeDict.TryGetValue(episode.AnimeID, out var anime) &&
                    user.AllowedAnime(anime) &&
                    (includeRestricted || anime.Restricted == 0) &&
                    (showAll || seriesDict.ContainsKey(episode.AnimeID)))
            .OrderBy(episode => episode.GetAirDateAsDate())
            .Select(episode =>
            {
                var anime = animeDict[episode.AnimeID];
                if (seriesDict.TryGetValue(episode.AnimeID, out var series))
                {
                    var xref = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID).MinBy(xref => xref.Percentage);
                    var file = xref != null ? RepoFactory.VideoLocal.GetByHash(xref.Hash) : null;
                    return new Dashboard.EpisodeDetails(episode, anime, series, file);
                }

                return new Dashboard.EpisodeDetails(episode, anime);
            })
            .ToList();
    }

    public DashboardController(ISettingsProvider settingsProvider, SeriesFactory seriesFactory, QueueHandler queueHandler, AnimeSeriesService seriesService, AnimeSeries_UserRepository seriesUser, VideoLocal_UserRepository vlUsers) : base(settingsProvider)
    {
        _seriesFactory = seriesFactory;
        _queueHandler = queueHandler;
        _seriesService = seriesService;
        _seriesUser = seriesUser;
        _vlUsers = vlUsers;
    }
}
