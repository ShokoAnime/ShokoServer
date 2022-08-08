using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class DashboardController : BaseController
    {
        /// <summary>
        /// Get the counters of various collection stats
        /// </summary>
        /// <returns></returns>
        [HttpGet("Stats")]
        public Dashboard.CollectionStats GetStats()
        {
            List<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a)).ToList();
            int seriesCount = series.Count;

            int groupCount = series.DistinctBy(a => a.AnimeGroupID).Count();

            List<SVR_AnimeEpisode> episodes = series.SelectMany(a => a.GetAnimeEpisodes()).ToList();

            List<SVR_VideoLocal> files = episodes.SelectMany(a => a?.GetVideoLocals()).Where(a => a != null)
                .DistinctBy(a => a.VideoLocalID).ToList();
            int fileCount = files.Count;
            long size = files.Sum(a => a.FileSize);

            List<SVR_AnimeEpisode> watchedEpisodes = episodes.Where(a => a?.GetUserRecord(User.JMMUserID)?.WatchedDate != null).ToList();

            int watchedSeries = RepoFactory.AnimeSeries.GetAll().Count(a =>
            {
                CL_AnimeSeries_User contract = a.GetUserContract(User.JMMUserID);
                if (contract == null) return false;
                return contract.WatchedEpisodeCount == a.GetAnimeEpisodesAndSpecialsCountWithVideoLocal();
            });

            decimal hours = Math.Round((decimal) watchedEpisodes.Select(a => a.GetVideoLocals().FirstOrDefault())
                .Where(a => a != null).Sum(a => a.Media?.GeneralStream?.Duration ?? 0) / 3600, 1, MidpointRounding.AwayFromZero); // Duration in s => 60m*60s = 3600

            List<SVR_VideoLocal_Place> places = files.SelectMany(a => a.Places).ToList();
            int duplicate = places.Where(a => a.VideoLocal.IsVariation == 0).SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.VideoLocal.Hash))
                .GroupBy(a => a.EpisodeID).Count(a => a.Count() > 1);

            decimal percentDupe = places.Count == 0 ? 0 : 
                Math.Round((decimal) duplicate * 100 / places.Count, 2, MidpointRounding.AwayFromZero);

            int missing = series.Sum(a => a.MissingEpisodeCount);
            int missingCollecting = series.Sum(a => a.MissingEpisodeCountGroups);

            int unrecognized = RepoFactory.VideoLocal.GetVideosWithoutEpisodeUnsorted().Count();

            int missingLinks = series.Count(MissingBothTvDBAndMovieDBLink);

            int multipleEps = episodes.Count(a => a.GetVideoLocals().Count(b => b.IsVariation == 0) > 1);

            int duplicateFiles = places.GroupBy(a => a.VideoLocalID).Count(a => a.Count() > 1);

            return new Dashboard.CollectionStats
            {
                FileCount = fileCount,
                FileSize = size,
                SeriesCount = seriesCount,
                GroupCount = groupCount,
                FinishedSeries = watchedSeries,
                WatchedEpisodes = watchedEpisodes.Count,
                WatchedHours = hours,
                PercentDuplicate = percentDupe,
                MissingEpisodes = missing,
                MissingEpisodesCollecting = missingCollecting,
                UnrecognizedFiles = unrecognized,
                SeriesWithMissingLinks = missingLinks,
                EpisodesWithMultipleFiles = multipleEps,
                FilesWithDuplicateLocations = duplicateFiles
            };
        }

        private static bool MissingBothTvDBAndMovieDBLink(SVR_AnimeSeries ser)
        {
            if (ser?.Contract == null || ser?.GetAnime() == null) return false;
            if (ser?.GetAnime()?.Restricted > 0) return false;
            // MovieDB is in AniDB_Other, and that's a Direct repository, so we don't want to call it on API
            bool movieLinkMissing = ser?.Contract.CrossRefAniDBMovieDB == null;
            bool tvlinkMissing =
                RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID).Count == 0;
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
        public List<Tag> GetTopTagsObsolete(int number = 10, [FromQuery] TagFilter.Filter filter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source)
            => GetTopTags(number, 0, filter);
            
        /// <summary>
        /// Gets the top number of the most common tags visible to the current user.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number.</param>
        /// <param name="filter">The <see cref="TagFilter.Filter" /> to use. (Defaults to <see cref="TagFilter.Filter.AnidbInternal" /> | <see cref="TagFilter.Filter.Misc" /> | <see cref="TagFilter.Filter.Source" />)</param>
        /// <returns></returns>
        [HttpGet("TopTags")]
        public List<Tag> GetTopTags([FromQuery] int pageSize = 10, [FromQuery] int page = 0, [FromQuery] TagFilter.Filter filter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source)
        {
            var tags = RepoFactory.AniDB_Anime_Tag.GetAllForLocalSeries().GroupBy(a => a.TagID)
                .ToDictionary(a => a.Key, a => a.Count()).OrderByDescending(a => a.Value)
                .Select(a => RepoFactory.AniDB_Tag.GetByTagID(a.Key))
                .Where(a => a != null && !User.GetHideCategories().Contains(a.TagName)).Select(a => new Tag
                {
                    Name = a.TagName,
                    Description = a.TagDescription,
                    Weight = 0
                }).ToList();
            if (pageSize <= 0)
                return new TagFilter<Tag>(tag => new Tag(tag), tag => tag.Name).ProcessTags(filter, tags).ToList();
            if (page <= 0) page = 0;
            return new TagFilter<Tag>(tag => new Tag(tag), tag => tag.Name).ProcessTags(filter, tags)
                .Skip(pageSize * page).Take(pageSize).ToList();
        }

        /// <summary>
        /// Gets counts for all of the commands currently queued
        /// </summary>
        /// <returns></returns>
        [HttpGet("QueueSummary")]
        public Dictionary<CommandRequestType, int> GetQueueSummary()
        {
            return RepoFactory.CommandRequest.GetAll().GroupBy(a => a.CommandType)
                .ToDictionary(a => (CommandRequestType) a.Key, a => a.Count());
        }

        /// <summary>
        /// Gets a breakdown of which types of anime the user has access to
        /// </summary>
        /// <returns></returns>
        [HttpGet("SeriesSummary")]
        public Dashboard.SeriesSummary GetSeriesSummary()
        {
            var series = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a)).GroupBy(a => (AnimeType) (a.GetAnime()?.AnimeType ?? -1))
                .ToDictionary(a => a.Key, a => a.Count());

            if (!series.TryGetValue(AnimeType.TVSeries, out int seriesCount)) seriesCount = 0;
            if (!series.TryGetValue(AnimeType.TVSpecial, out int specialCount)) specialCount = 0;
            if (!series.TryGetValue(AnimeType.Movie, out int movieCount)) movieCount = 0;
            if (!series.TryGetValue(AnimeType.OVA, out int ovaCount)) ovaCount = 0;
            if (!series.TryGetValue(AnimeType.Web, out int webCount)) webCount = 0;
            if (!series.TryGetValue(AnimeType.Other, out int otherCount)) otherCount = 0;
            if (!series.TryGetValue(AnimeType.None, out int noneCount)) noneCount = 0;
            return new Dashboard.SeriesSummary
            {
                Series = seriesCount,
                Special = specialCount,
                Movie = movieCount,
                OVA = ovaCount,
                Web = webCount,
                Other = otherCount,
                None = noneCount
            };
        }
        
        /// <summary>
        /// Get a list of recently added <see cref="Dashboard.EpisodeDetails"/>.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number.</param>
        /// <returns></returns>
        [HttpGet("RecentlyAddedEpisodes")]
        public List<Dashboard.EpisodeDetails> GetRecentlyAddedEpisodes([FromQuery] [Range(0, 100)] int pageSize = 30, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
        {
            var user = HttpContext.GetUser();
            var episodeList = RepoFactory.VideoLocal.GetAll()
                .OrderByDescending(f => f.DateTimeCreated)
                .SelectMany(file => file.GetAnimeEpisodes().Select(episode => (file, episode)));
            var seriesDict = episodeList
                .DistinctBy(tuple => tuple.episode.AnimeSeriesID)
                .Select(tuple => tuple.episode.GetAnimeSeries())
                .Where(series => series != null && user.AllowedSeries(series))
                .ToDictionary(series => series.AnimeSeriesID);
            var animeDict = seriesDict.Values
                .ToDictionary(series => series.AnimeSeriesID, series => series.GetAnime());

            if (pageSize <= 0)
                return episodeList
                    .Where(tuple => seriesDict.ContainsKey(tuple.episode.AnimeSeriesID))
                    .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, seriesDict[tuple.episode.AnimeSeriesID], animeDict[tuple.episode.AnimeSeriesID], tuple.file))
                    .ToList();
            return episodeList
                .Where(tuple => seriesDict.ContainsKey(tuple.episode.AnimeSeriesID))
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, seriesDict[tuple.episode.AnimeSeriesID], animeDict[tuple.episode.AnimeSeriesID], tuple.file))
                .ToList();
        }

        /// <summary>
        /// Get a list of recently added <see cref="Series"/>.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number.</param>
        /// <returns></returns>
        [HttpGet("RecentlyAddedSeries")]
        public List<Series> GetRecentlyAddedSeries([FromQuery] [Range(0, 100)] int pageSize = 20, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
        {
            var user = HttpContext.GetUser();
            var seriesList = RepoFactory.VideoLocal.GetAll()
                .OrderByDescending(f => f.DateTimeCreated)
                .SelectMany(file => file.GetAnimeEpisodes().Select(episode => episode.AnimeSeriesID))
                .Distinct()
                .Select(seriesID => RepoFactory.AnimeSeries.GetByID(seriesID))
                .Where(series => series != null && user.AllowedSeries(series));

            if (pageSize <= 0)
                return seriesList
                    .Select(a => new Series(HttpContext, a))
                    .ToList();
            return seriesList
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .Select(a => new Series(HttpContext, a))
                .ToList();
        }

        /// <summary>
        /// Get a list of the episodes to continue watching (soon-to-be) in recently watched order.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number.</param>
        /// <param name="includeSpecials">Include specials in the search.</param>
        /// <returns></returns>
        [HttpGet("ContinueWatchingEpisodes")]
        public List<Dashboard.EpisodeDetails> GetContinueWatchingEpisodes([FromQuery] [Range(0, 100)] int pageSize = 20, [FromQuery] [Range(0, Int16.MaxValue)] int page = 0, [FromQuery] bool includeSpecials = true)
        {
            var user = HttpContext.GetUser();
            var episodeList = RepoFactory.AnimeSeries_User.GetByUserID(user.JMMUserID)
                .Where(record => record.LastEpisodeUpdate.HasValue)
                .OrderByDescending(record => record.LastEpisodeUpdate)
                .Select(record => record.AnimeSeries)
                .Where(series => user.AllowedSeries(series))
                .Select(series => (series, series.GetActiveEpisode(user.JMMUserID, includeSpecials)))
                .Where(tuple => tuple.Item2 != null);
            if (pageSize <= 0)
                return episodeList
                    .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.Item2, tuple.series))
                    .ToList();
            return episodeList
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.Item2, tuple.series))
                .ToList();
        }

        /// <summary>
        /// Get the next episodes for series that currently don't have an active watch session for the user.
        /// </summary>
        /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
        /// <param name="page">Page number.</param>
        /// <param name="onlyUnwatched">Only show unwatched episodes.</param>
        /// <param name="includeSpecials">Include specials in the search.</param>
        /// <returns></returns>
        [HttpGet("NextUpEpisodes")]
        public List<Dashboard.EpisodeDetails> GetNextUpEpisodes([FromQuery] [Range(0, 100)] int pageSize = 20, [FromQuery] [Range(0, Int16.MaxValue)] int page = 0, [FromQuery] bool onlyUnwatched = true, [FromQuery] bool includeSpecials = true)
        {
            var user = HttpContext.GetUser();
            var episodeList = RepoFactory.AnimeSeries_User.GetByUserID(user.JMMUserID)
                .Where(record => record.LastEpisodeUpdate.HasValue && (onlyUnwatched ? record.UnwatchedEpisodeCount > 0 : true))
                .OrderByDescending(record => record.LastEpisodeUpdate)
                .Select(record => record.AnimeSeries)
                .Where(series => user.AllowedSeries(series))
                .Select(series => (series, series.GetNextEpisode(user.JMMUserID, onlyUnwatched, includeSpecials)))
                .Where(tuple => tuple.Item2 != null);
            if (pageSize <= 0)
                return episodeList
                    .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.Item2, tuple.series))
                    .ToList();
            return episodeList
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .Select(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.Item2, tuple.series))
                .ToList();
        }

        [NonAction]
        public Dashboard.EpisodeDetails GetEpisodeDetailsForSeriesAndEpisode(SVR_JMMUser user, SVR_AnimeEpisode episode, SVR_AnimeSeries series, SVR_AniDB_Anime anime = null, SVR_VideoLocal file = null)
        {
            SVR_VideoLocal_User userRecord = null;
            var animeEpisode = episode.AniDB_Episode;
            if (anime == null)
                anime = series.GetAnime();
            if (file != null)
                userRecord = file.GetUserRecord(user.JMMUserID);
            else
                (file, userRecord) = episode.GetVideoLocals()
                    .Select(file => (file, userRecord: file.GetUserRecord(user.JMMUserID)))
                    .OrderByDescending(tuple => tuple.userRecord?.LastUpdated)
                    .ThenByDescending(tuple => tuple.file.DateTimeCreated)
                .FirstOrDefault();
            return new Dashboard.EpisodeDetails(animeEpisode, anime, series, file, userRecord);
        }

        /// <summary>
        /// Get the next <paramref name="numberOfDays"/> from the AniDB Calendar.
        /// </summary>
        /// <param name="numberOfDays">Number of days to show.</param>
        /// <param name="showAll">Show all series.</param>
        /// <returns></returns>
        [HttpGet("AniDBCalendar")]
        public List<Dashboard.EpisodeDetails> GetAniDBCalendarInDays([FromQuery] int numberOfDays = 7, [FromQuery] bool showAll = false)
        {
            SVR_JMMUser user = HttpContext.GetUser();
            var episodeList = RepoFactory.AniDB_Episode.GetForDate(DateTime.Today, DateTime.Today.AddDays(numberOfDays)).ToList();
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
                .Where(episode => user.AllowedAnime(animeDict[episode.AnimeID]) && (showAll || seriesDict.ContainsKey(episode.AnimeID)))
                .OrderBy(episode => episode.GetAirDateAsDate())
                .Select(episode =>
                {
                    var anime = animeDict[episode.AnimeID];
                    if (seriesDict.TryGetValue(episode.AnimeID, out var series))
                    {
                        var xref = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID)
                            .OrderBy(xref => xref.Percentage)
                            .FirstOrDefault();
                        var file = xref != null ? RepoFactory.VideoLocal.GetByHash(xref.Hash) : null;
                        return new Dashboard.EpisodeDetails(episode, anime, series, file);
                    }
                    return new Dashboard.EpisodeDetails(episode, anime);
                })
                .ToList();
        }
    }
}