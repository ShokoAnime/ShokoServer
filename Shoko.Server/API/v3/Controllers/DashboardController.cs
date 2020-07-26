using System;
using System.Collections.Generic;
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
        /// Get the Top 10 Most Common Tags that are visible to the user
        /// </summary>
        /// <returns></returns>
        [HttpGet("TopTags")]
        public List<Tag> GetTopTags()
        {
            return GetTopTags(10);
        }

        /// <summary>
        /// Gets the top <para>number</para> most common tags visible to the current user 
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        [HttpGet("TopTags/{number}")]
        public List<Tag> GetTopTags(int number)
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
            var tagfilter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source;
            tags = TagFilter.ProcessTags(tagfilter, tags, tag => tag.Name).Take(10).ToList();
            return tags;
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
    }
}