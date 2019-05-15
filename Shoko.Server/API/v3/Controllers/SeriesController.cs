using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class SeriesController : BaseController
    {
        /// <summary>
        /// Get a list of all series available to the current user
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<Series>> GetAllSeries()
        {
            var allSeries = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a)).ToList();
            return allSeries.Select(a => new Series(HttpContext, a)).ToList();
        }

        /// <summary>
        /// Get the series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<Series> GetSeries(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return new Series(HttpContext, ser);
        }

        /// <summary>
        /// Get AniDB Info for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/AniDB")]
        public ActionResult<Series.AniDB> GetSeriesAniDBDetails(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.GetAniDBInfo(HttpContext, anime);
        }
        
        /// <summary>
        /// Get TvDB Info for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/TvDB")]
        public ActionResult<List<Series.TvDB>> GetSeriesTvDBDetails(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetTvDBInfo(HttpContext, ser);
        }
        
        /// <summary>
        /// Get all images for series with ID, optionally with Disabled images, as well.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includeDisabled"></param>
        /// <returns></returns>
        [HttpGet("{id}/Images/{IncludeDisabled?}")]
        public ActionResult<Images> GetSeriesImages(int id, bool includeDisabled)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetArt(HttpContext, ser.AniDB_ID, includeDisabled);
        }
        
        /// <summary>
        /// Get tags for Series with ID, applying the given TagFilter (0 is show all)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpGet("{id}/Tags/{filter}")]
        public ActionResult<List<Tag>> GetSeriesTags(int id, TagFilter.Filter filter)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.GetTags(HttpContext, anime, filter);
        }
        
        /// <summary>
        /// Get the cast listing for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/Cast")]
        public ActionResult<List<Role>> GetSeriesCast(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetCast(HttpContext, ser.AniDB_ID);
        }
        
        /// <summary>
        /// Get the group for a series ID
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Group")]
        public ActionResult<Group> GetGroup(int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return BadRequest("No Series with ID");
            var grp = series.AnimeGroup;
            if (grp == null) return BadRequest("No Group for Series");
            return new Group(HttpContext, grp);
        }

        /// <summary>
        /// Move the series to a new group, and update accordingly
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="newGroupID"></param>
        /// <returns></returns>
        [HttpPatch("{seriesID}/Move/{newGroupID}")]
        public ActionResult MoveSeries(int seriesID, int newGroupID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return BadRequest("No Series with ID");
            var grp = RepoFactory.AnimeGroup.GetByID(newGroupID);
            if (grp == null) return BadRequest("No Group with ID");
            series.MoveSeries(grp);
            return Ok();
        }

        #region internal function

        /// <summary>
        /// function used in fuzzy search
        /// </summary>
        /// <param name="search"></param>
        /// <param name="languages"></param>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        [NonAction]
        private static void CheckTitlesFuzzy(IStringDistance search, HashSet<string> languages, SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, Tuple<double, string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AnimeTitles == null) return;
            var dist = double.MaxValue;
            string match = string.Empty;

            var seriesTitles = a.Contract.AniDBAnime.AnimeTitles
                .Where(b => languages.Contains(b.Language.ToLower()) &&
                            b.TitleType != Shoko.Models.Constants.AnimeTitleType.ShortName).Select(b => b.Title)
                .ToList();
            foreach (string title in seriesTitles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                var result = 0.0;
                // Check for exact match
                if (!title.Equals(query, StringComparison.Ordinal))
                    result = search.Distance(title, query);
                // For Dice, 1 is no reasonable match
                if (result >= 1) continue;
                // Don't count an error as liberally when the title is short
                if (title.Length < 5 && result > 0.8) continue;
                if (result < dist)
                {
                    match = title;
                    dist = result;
                } else if (Math.Abs(result - dist) < 0.00001)
                {
                    if (title.Length < match.Length) match = title;
                }
            }
            // Keep the lowest distance, then by shortest title
            if (dist < double.MaxValue)
                distLevenshtein.AddOrUpdate(a, new Tuple<double, string>(dist, match),
                    (key, oldValue) =>
                    {
                        if (oldValue.Item1 < dist) return oldValue;
                        if (Math.Abs(oldValue.Item1 - dist) < 0.00001)
                            return oldValue.Item2.Length < match.Length
                                ? oldValue
                                : new Tuple<double, string>(dist, match);

                        return new Tuple<double, string>(dist, match);
                    });
        }

        class SearchGrouping
        {
            public List<SVR_AnimeSeries> Series { get; set; }
            public double Distance { get; set; }
            public string Match { get; set; }
        }

        /// <summary>
        /// Search for series with given query in name or tag
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="limit">number of return items</param>
        /// <returns>List<see cref="SeriesSearchResult"/></returns>
        [HttpGet("Search/{query}")]
        public ActionResult<IEnumerable<SeriesSearchResult>> Search(string query, int limit = int.MaxValue)
        {
            SorensenDice search = new SorensenDice();
            query = query.ToLowerInvariant();
            query = query.Replace("+", " ");

            List<SeriesSearchResult> seriesList = new List<SeriesSearchResult>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(User.GetHideCategories()))
                .AsParallel();

            HashSet<string> languages = new HashSet<string>{"en", "x-jat"};
            languages.UnionWith(ServerSettings.Instance.LanguagePreference);
            var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<double, string>>();
            allSeries.ForAll(a => CheckTitlesFuzzy(search, languages, a, query, ref distLevenshtein, limit));

            var tempListToSort = distLevenshtein.Keys.GroupBy(a => a.AnimeGroupID).Select(a =>
            {
                var tempSeries = a.ToList();
                tempSeries.Sort((j, k) =>
                {
                    var result1 = distLevenshtein[j];
                    var result2 = distLevenshtein[k];
                    var exactMatch = result1.Item1.CompareTo(result2.Item1);
                    if (exactMatch != 0) return exactMatch;

                    string title1 = j.GetSeriesName();
                    string title2 = k.GetSeriesName();
                    if (title1 == null && title2 == null) return 0;
                    if (title1 == null) return 1;
                    if (title2 == null) return -1;
                    return String.Compare(title1, title2, StringComparison.InvariantCultureIgnoreCase);
                });
                var result = new SearchGrouping
                {
                    Series = a.OrderBy(b => b.AirDate).ToList(),
                    Distance = distLevenshtein[tempSeries[0]].Item1,
                    Match = distLevenshtein[tempSeries[0]].Item2
                };
                return result;
            });

            Dictionary<SVR_AnimeSeries, Tuple<double, string>> series = tempListToSort.OrderBy(a => a.Distance)
                .ThenBy(a => a.Match.Length).SelectMany(a => a.Series).ToDictionary(a => a, a => distLevenshtein[a]);
            foreach (KeyValuePair<SVR_AnimeSeries, Tuple<double, string>> ser in series)
            {
                seriesList.Add(new SeriesSearchResult(HttpContext, ser.Key, ser.Value.Item2, ser.Value.Item1));
                if (seriesList.Count >= limit)
                {
                    break;
                }
            }

            return seriesList;
        }

        [NonAction]
        private static void CheckTitlesStartsWith(SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, string> series, int limit)
        {
            if (series.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return;
            string match = string.Empty;
            foreach (string title in a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title).ToList())
            {
                if (string.IsNullOrEmpty(title)) continue;
                if (title.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                {
                    match = title;
                }
            }
            // Keep the lowest distance
            if (match != string.Empty)
                series.TryAdd(a, match);
        }

        /// <summary>
        /// Searches for series whose title starts with a string
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        [HttpGet("StartsWith/{query}")]
        public ActionResult<List<SeriesSearchResult>> StartsWith(string query, int limit = int.MaxValue)
        {
            query = query.ToLowerInvariant();

            List<SeriesSearchResult> seriesList = new List<SeriesSearchResult>();
            ConcurrentDictionary<SVR_AnimeSeries, string> tempSeries = new ConcurrentDictionary<SVR_AnimeSeries, string>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(User.GetHideCategories()))
                .AsParallel();

            #region Search_TitlesOnly
            allSeries.ForAll(a => CheckTitlesStartsWith(a, query, ref tempSeries, limit));
            Dictionary<SVR_AnimeSeries, string> series =
                tempSeries.OrderBy(a => a.Value).ToDictionary(a => a.Key, a => a.Value);

            foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
            {
                seriesList.Add(new SeriesSearchResult(HttpContext, ser.Key, ser.Value, 0));
                if (seriesList.Count >= limit)
                {
                    break;
                }
            }

            #endregion

            return seriesList;
        }

        #endregion
    }
}
