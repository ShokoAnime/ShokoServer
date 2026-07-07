using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class DashboardController(
    ISettingsProvider settingsProvider,
    AnimeSeriesService _seriesService,
    AnimeSeries_UserRepository _seriesUser,
    AnimeEpisode_UserRepository _animeEpisodeUser,
    VideoLocal_UserRepository _vlUsers,
    AniDB_AnimeRepository _anidbAnimes,
    AniDB_Anime_TagRepository _anidbAnimeTags,
    AniDB_EpisodeRepository _anidbEpisodes,
    AniDB_TagRepository _anidbTags,
    AnimeSeriesRepository _animeSeries,
    AnimeEpisodeRepository _animeEpisodes,
    CrossRef_AniDB_TMDB_MovieRepository _crossRefAnidbTmdbMovies,
    CrossRef_AniDB_TMDB_ShowRepository _crossRefAnidbTmdbShows,
    CrossRef_File_EpisodeRepository _crossRefFileEpisodes,
    VideoLocalRepository _videoLocals,
    VideoLocal_PlaceRepository _videoLocalPlaces
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get the counters of various collection stats
    /// </summary>
    /// <returns></returns>
    [HttpGet("Stats")]
    public Dashboard.CollectionStats GetStats()
    {
        var userId = User.JMMUserID;

        // Allowed series and their identifiers.
        var allSeries = _animeSeries.GetAll()
            .Where(User.AllowedSeries)
            .ToList();
        var allowedAnimeIDs = allSeries.Select(s => s.AniDB_ID).ToHashSet();
        var allowedSeriesIDs = allSeries.Select(s => s.AnimeSeriesID).ToHashSet();
        var groupCount = allSeries.DistinctBy(a => a.AnimeGroupID).Count();

        // All cross-refs once — split into per-series subset and full hash set for unrecognized detection.
        var allCrossRefs = _crossRefFileEpisodes.GetAll();
        var allCrossRefHashes = allCrossRefs.Select(x => x.Hash).ToHashSet();
        var crossRefs = allCrossRefs.Where(xref => allowedAnimeIDs.Contains(xref.AnimeID)).ToList();

        // Build hash → VideoLocal from a single GetAll() instead of one ReadLock per distinct hash.
        var allowedHashes = crossRefs.Select(x => x.Hash).ToHashSet();
        var allVideoLocals = _videoLocals.GetAll();
        var fileByHash = allVideoLocals
            .Where(f => !string.IsNullOrEmpty(f.Hash) && allowedHashes.Contains(f.Hash))
            .GroupBy(f => f.Hash)
            .ToDictionary(g => g.Key, g => g.First());
        var totalFileSize = fileByHash.Values.Sum(f => f.FileSize);

        // Unrecognized files — computed from already-loaded data, avoiding GetVideosWithoutEpisodeUnsorted()
        // which would call CrossRef_File_Episode.GetByEd2k() (ReadLock) for every video local.
        var unrecognizedFiles = allVideoLocals.Count(f => !f.IsIgnored && !string.IsNullOrEmpty(f.Hash) && !allCrossRefHashes.Contains(f.Hash));

        // Places — one GetAll() + filter instead of one ReadLock per file via VideoLocal.Places.
        var collectionFileIds = fileByHash.Values.Select(f => f.VideoLocalID).ToHashSet();
        var places = _videoLocalPlaces.GetAll()
            .Where(p => collectionFileIds.Contains(p.VideoID))
            .ToList();

        // Episodes with >1 distinct non-variation file, and the duplicate-files percentage denominator.
        var episodeFileCounts = crossRefs
            .Where(xref => fileByHash.TryGetValue(xref.Hash, out var f) && !f.IsVariation)
            .GroupBy(xref => xref.EpisodeID)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Hash).Distinct().Count());
        var multipleEpisodes = episodeFileCounts.Values.Count(c => c > 1);
        var duplicates = multipleEpisodes;
        var percentDuplicates = places.Count == 0
            ? 0
            : Math.Round((decimal)duplicates * 100 / places.Count, 2, MidpointRounding.AwayFromZero);

        // All episodes in allowed series.
        var allEpisodes = _animeEpisodes.GetAll()
            .Where(e => allowedSeriesIDs.Contains(e.AnimeSeriesID))
            .ToList();

        // AniDB episode data in one GetAll() — avoids per-episode GetByEpisodeID ReadLock calls when
        // building episodeTypeById and the hours-watched fallback length lookup.
        var anidbEpById = _anidbEpisodes.GetAll()
            .Where(e => allowedAnimeIDs.Contains(e.AnimeID))
            .ToDictionary(e => e.EpisodeID);
        var anidbEpIdByAnimeEpId = allEpisodes.ToDictionary(e => e.AnimeEpisodeID, e => e.AniDB_EpisodeID);
        var episodeTypeById = allEpisodes.ToDictionary(
            e => e.AnimeEpisodeID,
            e => anidbEpById.TryGetValue(e.AniDB_EpisodeID, out var ae) ? ae.EpisodeType : EpisodeType.Other);
        var episodeLengthByAnimeEpId = allEpisodes.ToDictionary(
            e => e.AnimeEpisodeID,
            e => anidbEpById.TryGetValue(e.AniDB_EpisodeID, out var ae) ? ae.LengthSeconds : 0);

        // Episode user records for the user.
        var watchedEpisodeRecords = _animeEpisodeUser.GetByUserID(userId)
            .Where(r => r.WatchedDate.HasValue && allowedSeriesIDs.Contains(r.AnimeSeriesID))
            .ToList();

        // First file per AniDB episode ID for watched hours (keyed by AniDB episode ID).
        var firstFileByAnidbEpId = crossRefs
            .GroupBy(x => x.EpisodeID)
            .ToDictionary(g => g.Key, g => fileByHash.TryGetValue(g.MinBy(x => x.EpisodeOrder)!.Hash, out var vl) ? vl : null);

        var hoursWatched = Math.Round(
            (decimal)watchedEpisodeRecords.Sum(r =>
            {
                if (anidbEpIdByAnimeEpId.TryGetValue(r.AnimeEpisodeID, out var anidbEpId) &&
                    firstFileByAnidbEpId.TryGetValue(anidbEpId, out var file) && file is not null)
                    return file.DurationTimeSpan.TotalHours;
                return new TimeSpan(0, 0, episodeLengthByAnimeEpId.GetValueOrDefault(r.AnimeEpisodeID)).TotalHours;
            }),
            1, MidpointRounding.AwayFromZero);

        // Pre-load AniDB anime and TMDB cross-ref sets to avoid per-series ReadLock calls in
        // watchedSeries and seriesWithMissingLinks loops.
        var anidbAnimeById = _anidbAnimes.GetAll()
            .Where(a => allowedAnimeIDs.Contains(a.AnimeID))
            .ToDictionary(a => a.AnimeID);
        var tmdbMovieAnimeIDs = _crossRefAnidbTmdbMovies.GetAll().Select(x => x.AnidbAnimeID).ToHashSet();
        var tmdbShowAnimeIDs = _crossRefAnidbTmdbShows.GetAll().Select(x => x.AnidbAnimeID).ToHashSet();

        var userSeriesIDs = _seriesUser.GetByUserID(userId).Select(r => r.AnimeSeriesID).ToHashSet();
        var watchedNormalCountBySeries = watchedEpisodeRecords
            .GroupBy(r => r.AnimeSeriesID)
            .ToDictionary(g => g.Key,
                g => g.Count(r => episodeTypeById.TryGetValue(r.AnimeEpisodeID, out var et) && et == EpisodeType.Episode));

        var watchedSeries = allSeries.Count(series =>
        {
            if (!anidbAnimeById.TryGetValue(series.AniDB_ID, out var anime))
                return false;

            if (anime.EpisodeCountNormal == 0)
                return false;

            var missingNormalEpisodesTotal = series.MissingEpisodeCount + series.HiddenMissingEpisodeCount;
            if (anime.EpisodeCountNormal == missingNormalEpisodesTotal)
                return false;

            if (!userSeriesIDs.Contains(series.AnimeSeriesID))
                return false;

            var totalWatchableNormalEpisodes = anime.EpisodeCountNormal - missingNormalEpisodesTotal;
            var watchedNormalCount = watchedNormalCountBySeries.GetValueOrDefault(series.AnimeSeriesID);
            return watchedNormalCount >= totalWatchableNormalEpisodes;
        });

        var missingEpisodes = allSeries.Sum(s => s.MissingEpisodeCount);
        var missingEpisodesCollecting = allSeries.Sum(s => s.MissingEpisodeCountGroups);
        var duplicateFiles = places.GroupBy(p => p.VideoID).Count(g => g.Count() > 1);
        var seriesWithMissingLinks = allSeries.Count(series =>
        {
            if (series.IsTmdbAutoMatchingDisabled)
                return false;
            var animeType = anidbAnimeById.TryGetValue(series.AniDB_ID, out var a) ? a.AnimeType : AnimeType.Unknown;
            if (MissingTmdbLinkExpression.AnimeTypes.Contains(animeType))
                return false;
            return !tmdbMovieAnimeIDs.Contains(series.AniDB_ID) && !tmdbShowAnimeIDs.Contains(series.AniDB_ID);
        });

        return new()
        {
            FileCount = fileByHash.Count,
            FileSize = totalFileSize,
            SeriesCount = allSeries.Count,
            GroupCount = groupCount,
            FinishedSeries = watchedSeries,
            WatchedEpisodes = watchedEpisodeRecords.Count,
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

    /// <summary>
    /// Gets the top number of the most common tags visible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="filter">The <see cref="TagFilter.Filter" /> to use. (Defaults to <see cref="TagFilter.Filter.AnidbInternal" /> | <see cref="TagFilter.Filter.Misc" /> | <see cref="TagFilter.Filter.Source" />)</param>
    /// <returns></returns>
    [HttpGet("TopTags")]
    public List<Tag> GetTopTags(
        [FromQuery, Range(0, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] TagFilter.Filter filter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source
    )
    {
        var tags = _anidbAnimeTags.GetAllForLocalSeries()
            .GroupBy(xref => xref.TagID)
            .Select(xrefList => (tag: _anidbTags.GetByTagID(xrefList.Key), weight: xrefList.Count()))
            .Where(tuple => tuple.tag is not null && User.AllowedTag(tuple.tag))
            .OrderByDescending(tuple => tuple.weight)
            .Select(tuple => new Tag(tuple.tag!, true) { Weight = tuple.weight })
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
    /// Gets a breakdown of which types of anime the user has access to
    /// </summary>
    /// <returns></returns>
    [HttpGet("SeriesSummary")]
    public Dashboard.SeriesSummary GetSeriesSummary()
    {
        var series = _animeSeries.GetAll()
            .Where(User.AllowedSeries)
            .GroupBy(a => a.AniDB_Anime?.AnimeType ?? ((AnimeType)0x42))
            .ToDictionary(a => a.Key, a => a.Count());

        return new Dashboard.SeriesSummary
        {
            Series = series.GetValueOrDefault(AnimeType.TV, 0),
            Special = series.GetValueOrDefault(AnimeType.TVSpecial, 0),
            Movie = series.GetValueOrDefault(AnimeType.Movie, 0),
            OVA = series.GetValueOrDefault(AnimeType.OVA, 0),
            Web = series.GetValueOrDefault(AnimeType.Web, 0),
            Other = series.GetValueOrDefault(AnimeType.Other, 0),
            MusicVideo = series.GetValueOrDefault(AnimeType.MusicVideo, 0),
            Unknown = series.GetValueOrDefault(AnimeType.Unknown, 0),
            None = series.GetValueOrDefault((AnimeType)0x42, 0),
        };
    }

    /// <summary>
    /// Get a list of recently added <see cref="Dashboard.Episode"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedEpisodes")]
    public ListResult<Dashboard.Episode> GetRecentlyAddedEpisodes(
        [FromQuery, Range(0, 1000)] int pageSize = 30,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        var episodeList = _videoLocals.GetAll()
            .Where(f => f.DateTimeImported.HasValue)
            .OrderByDescending(f => f.DateTimeImported)
            .SelectMany(file => file.AnimeEpisodes.Select(episode => (file, episode)))
            .ToList();
        var seriesDict = episodeList
            .DistinctBy(tuple => tuple.episode.AnimeSeriesID)
            .Select(tuple => tuple.episode.AnimeSeries)
            .WhereNotNull()
            .Where(user.AllowedSeries)
            .ToDictionary(series => series.AnimeSeriesID);
        var animeDict = seriesDict.Values
            .ToDictionary(series => series.AnimeSeriesID, series => series.AniDB_Anime!);
        return episodeList
            .Where(tuple =>
            {
                if (!animeDict.TryGetValue(tuple.episode.AnimeSeriesID, out var anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .ToListResult(
                tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, seriesDict[tuple.episode.AnimeSeriesID], animeDict[tuple.episode.AnimeSeriesID], tuple.file),
                page,
                pageSize
            );
    }

    /// <summary>
    /// Get a list of recently added <see cref="Series"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedSeries")]
    public ListResult<Series> GetRecentlyAddedSeries(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();

        // Build hash → importDate map (one scan over all video locals)
        var fileImportDates = _videoLocals.GetAll()
            .Where(f => f.DateTimeImported.HasValue && !string.IsNullOrEmpty(f.Hash))
            .ToDictionary(f => f.Hash, f => f.DateTimeImported!.Value);

        // Build AniDB episodeID → AnimeSeriesID map (one scan over all AnimeEpisodes)
        var episodeSeriesMap = _animeEpisodes.GetAll()
            .DistinctBy(e => e.AniDB_EpisodeID)
            .ToDictionary(e => e.AniDB_EpisodeID, e => e.AnimeSeriesID);

        // Compute series → most recent file import date via cross-refs,
        // avoiding 67,804 per-file GetByEd2k lookups
        return _crossRefFileEpisodes.GetAll()
            .Where(xref => fileImportDates.ContainsKey(xref.Hash))
            .Select(xref => (
                SeriesID: episodeSeriesMap.TryGetValue(xref.EpisodeID, out var sid) ? sid : 0,
                ImportDate: fileImportDates[xref.Hash]
            ))
            .Where(t => t.SeriesID != 0)
            .GroupBy(t => t.SeriesID)
            .Select(g => (SeriesID: g.Key, LastImport: g.Max(t => t.ImportDate)))
            .OrderByDescending(x => x.LastImport)
            .Select(x => _animeSeries.GetByID(x.SeriesID))
            .Where(series =>
            {
                if (series?.AniDB_Anime is not { } anime || !user.AllowedAnime(anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .ToListResult(a => new Series(a, User.JMMUserID), page, pageSize);
    }

    /// <summary>
    /// Get a list of the episodes to continue watching (soon-to-be) in recently watched order.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("ContinueWatchingEpisodes")]
    public ListResult<Dashboard.Episode> GetContinueWatchingEpisodes(
        [FromQuery, Range(0, 100)] int pageSize = 20,
        [FromQuery, Range(0, int.MaxValue)] int page = 0,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeOthers = false,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        return _seriesUser.GetByUserID(user.JMMUserID)
            .Where(record => record.LastVideoUpdate.HasValue)
            .Select(record => _animeSeries.GetByID(record.AnimeSeriesID))
            .Where(series =>
            {
                if (series?.AniDB_Anime is not { } anime || !user.AllowedAnime(anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .Select(series =>
            {
                var (episode, video, videoUserData) = _seriesService.GetActiveEpisode(series, user.JMMUserID, includeSpecials, includeOthers);
                return (series, episode, video, videoUserData);
            })
            .Where(tuple => tuple.episode is not null && tuple.video is not null)
            .OrderByDescending(tuple => tuple.videoUserData!.LastUpdated)
            .ToListResult(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode!, tuple.series, file: tuple.video), page, pageSize);
    }

    /// <summary>
    /// Get the next episodes for series that currently don't have an active watch session for the user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="onlyUnwatched">Only show unwatched episodes.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <returns></returns>
    [HttpGet("NextUpEpisodes")]
    public ListResult<Dashboard.Episode> GetNextUpEpisodes(
        [FromQuery, Range(0, 100)] int pageSize = 20,
        [FromQuery, Range(0, int.MaxValue)] int page = 0,
        [FromQuery] bool onlyUnwatched = true,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeOthers = false,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery] bool includeMissing = false,
        [FromQuery] bool includeRewatching = false
    )
    {
        var user = HttpContext.GetUser();
        return _seriesUser.GetByUserID(user.JMMUserID)
            .Where(record => record.LastVideoUpdate.HasValue) // Filter to only series where the user have interacted with a video.
            .Select(record => (series: _animeSeries.GetByID(record.AnimeSeriesID), record))
            .Where(tuple =>
            {
                if (tuple.series?.AniDB_Anime is not { } anime || !user.AllowedAnime(anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .Select(tuple =>
            {
                var (series, seriesUserData) = tuple;
                var (episode, video) = _seriesService.GetNextUpEpisode(
                    series,
                    user.JMMUserID,
                    new()
                    {
                        DisableFirstEpisode = true,
                        IncludeCurrentlyWatching = !onlyUnwatched,
                        IncludeMissing = includeMissing,
                        IncludeRewatching = includeRewatching,
                        IncludeSpecials = includeSpecials,
                        IncludeOthers = includeOthers,
                    }
                );
                var videoUserData = video is not null
                    ? _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, video.VideoLocalID)
                    : null;
                // Order the episodes either by the last time the user viewed the video or whichever is highest of
                // the last episode added to collection date and the last episode user data update date.
                var orderDate = videoUserData?.LastUpdated ?? (
                    seriesUserData.LastEpisodeUpdate.HasValue && (
                        !series.EpisodeAddedDate.HasValue ||
                        seriesUserData.LastEpisodeUpdate > series.EpisodeAddedDate
                    )
                        ? seriesUserData.LastEpisodeUpdate.Value
                        : series.EpisodeAddedDate ?? DateTime.MinValue
                );
                return (series, episode, video, orderDate);
            })
            .Where(tuple => tuple.episode is not null)
            .OrderByDescending(tuple => tuple.orderDate)
            .ToListResult(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode!, tuple.series, file: tuple.video), page, pageSize);
    }

    [NonAction]
    public Dashboard.Episode GetEpisodeDetailsForSeriesAndEpisode(
        JMMUser user,
        AnimeEpisode episode,
        AnimeSeries series,
        AniDB_Anime? anime = null,
        VideoLocal? file = null
        )
    {
        VideoLocal_User? userRecord;
        var animeEpisode = episode.AniDB_Episode!;
        anime ??= series.AniDB_Anime!;

        if (file is not null)
        {
            userRecord = _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, file.VideoLocalID);
        }
        else
        {
            (file, userRecord) = episode.VideoLocals
                .Select(f => (file: f, userRecord: _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, f.VideoLocalID)))
                .OrderByDescending(tuple => tuple.userRecord?.LastUpdated)
                .ThenByDescending(tuple => tuple.file.DateTimeCreated)
                .FirstOrDefault();
        }

        return new Dashboard.Episode(animeEpisode, anime, series, file, userRecord);
    }

    /// <summary>
    /// Get the next <paramref name="numberOfDays"/> from the AniDB Calendar.
    /// </summary>
    /// <param name="numberOfDays">Number of days to show.</param>
    /// <param name="showAll">Show all series.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("AniDBCalendar")]
    public List<Dashboard.Episode> GetAniDBCalendarInDays([FromQuery] int numberOfDays = 7,
        [FromQuery] bool showAll = false, [FromQuery] bool includeRestricted = false)
        => GetCalendarEpisodes(
            DateTime.Today.ToDateOnly(),
            DateTime.Today.ToDateOnly().AddDays(numberOfDays),
            showAll ? IncludeOnlyFilter.True : IncludeOnlyFilter.False,
            includeRestricted ? IncludeOnlyFilter.True : IncludeOnlyFilter.False
        );

    /// <summary>
    /// Get the episodes within the given time-frame on the calendar.
    /// </summary>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="includeMissing">Include missing episodes.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("CalendarEpisodes")]
    public List<Dashboard.Episode> GetCalendarEpisodes(
        [FromQuery] DateOnly startDate = default,
        [FromQuery] DateOnly endDate = default,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        var episodeList = _anidbEpisodes.GetForDate(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified))
            .ToList();
        var animeDict = episodeList
            .Select(episode => _anidbAnimes.GetByAnimeID(episode.AnimeID))
            .WhereNotNull()
            .Distinct()
            .ToDictionary(anime => anime.AnimeID);
        var seriesDict = animeDict.Values
            .Select(anime => _animeSeries.GetByAnimeID(anime.AnimeID))
            .WhereNotNull()
            .Distinct()
            .ToDictionary(anime => anime.AniDB_ID);
        return episodeList
            .Where(episode =>
            {
                if (!animeDict.TryGetValue(episode.AnimeID, out var anime) || !user.AllowedAnime(anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                if (includeMissing is not IncludeOnlyFilter.True)
                {
                    var shouldHideMissing = includeMissing is IncludeOnlyFilter.False;
                    var isMissing = !seriesDict.ContainsKey(episode.AnimeID);
                    if (shouldHideMissing == isMissing)
                        return false;
                }

                return true;
            })
            .OrderBy(episode => episode.GetAirDateAsDate())
            .Select(episode =>
            {
                var anime = animeDict[episode.AnimeID];
                if (seriesDict.TryGetValue(episode.AnimeID, out var series))
                {
                    var xref = _crossRefFileEpisodes.GetByEpisodeID(episode.EpisodeID).MinBy(xref => xref.Percentage);
                    var file = xref?.VideoLocal;
                    return new Dashboard.Episode(episode, anime, series, file);
                }

                return new Dashboard.Episode(episode, anime);
            })
            .ToList();
    }
}
