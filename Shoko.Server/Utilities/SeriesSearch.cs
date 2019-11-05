using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Utilities
{
    public static class SeriesSearch
    {
        [Flags]
        public enum SearchFlags
        {
            Tags = 1,
            Titles = 2,
            Fuzzy = 4
        }

        private static readonly char[] InvalidPathChars =
            $"{new string(Path.GetInvalidFileNameChars())}{new string(Path.GetInvalidPathChars())}()+".ToCharArray();

        private static readonly char[] ReplaceWithSpace = @"[-.]".ToCharArray();

        public static string SanitizeFuzzy(string value, bool replaceInvalid)
        {
            if (!replaceInvalid) return value;

            value = value.FilterCharacters(InvalidPathChars, true);
            value = ReplaceWithSpace.Aggregate(value, (current, c) => current.Replace(c, ' '));

            return value.CompactWhitespaces();
        }

        /// <summary>
        ///     function used in fuzzy search
        /// </summary>
        /// <param name="grouping"></param>
        /// <param name="query"></param>
        private static SearchGrouping CheckTitlesFuzzy(IGrouping<int, SVR_AnimeSeries> grouping, string query)
        {
            if (!(grouping?.SelectMany(a => a.GetAllTitles()).Any() ?? false)) return null;
            SearchGrouping dist = null;

            foreach (SVR_AnimeSeries item in grouping)
            foreach (string title in item.GetAllTitles())
            {
                if (string.IsNullOrEmpty(title)) continue;
                int k = Math.Max(Math.Min((int) (title.Length / 6D), (int) (query.Length / 6D)), 1);
                if (query.Length <= 4 || title.Length <= 4) k = 0;
                Misc.SearchInfo<IGrouping<int, SVR_AnimeSeries>> result =
                    Misc.DiceFuzzySearch(title, query, k, grouping);
                if (result.Index == -1) continue;
                SearchGrouping searchGrouping = new SearchGrouping
                {
                    Distance = result.Distance,
                    Index = result.Index,
                    ExactMatch = result.ExactMatch,
                    Match = title,
                    Results = grouping.OrderBy(a => a.AirDate).ToList()
                };
                if (result.Distance < (dist?.Distance ?? int.MaxValue)) dist = searchGrouping;
            }

            return dist;
        }

        /// <summary>
        ///     function used in fuzzy search
        /// </summary>
        /// <param name="grouping"></param>
        /// <param name="query"></param>
        private static SearchGrouping CheckTitlesIndexOf(IGrouping<int, SVR_AnimeSeries> grouping, string query)
        {
            if (!(grouping?.SelectMany(a => a.GetAllTitles()).Any() ?? false)) return null;
            SearchGrouping dist = null;

            foreach (SVR_AnimeSeries item in grouping)
            foreach (string title in item.GetAllTitles())
            {
                if (string.IsNullOrEmpty(title)) continue;
                int result = title.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (result == -1) continue;
                SearchGrouping searchGrouping = new SearchGrouping
                {
                    Distance = 0,
                    Index = result,
                    ExactMatch = true,
                    Match = title,
                    Results = grouping.OrderBy(a => a.AirDate).ToList()
                };
                if (result < (dist?.Index ?? int.MaxValue)) dist = searchGrouping;
            }

            return dist;
        }

        /// <summary>
        ///     Search for series with given query in name or tag
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="userID">user id</param>
        /// <param name="limit">The number of results to return</param>
        /// <param name="flags">The SearchFlags to determine the type of search</param>
        /// <returns>
        ///     <see cref="List{SearchResult}" />
        /// </returns>
        public static List<SearchResult> Search(int userID, string query, int limit, SearchFlags flags)
        {
            query = query.ToLowerInvariant();

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) throw new Exception("User not found");
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll().AsParallel().Where(a =>
                a?.GetAnime() != null && !a.GetAnime().GetAllTags().FindInEnumerable(user.GetHideCategories()));
            ParallelQuery<AniDB_Tag> allTags = RepoFactory.AniDB_Tag.GetAll().AsParallel()
                .Where(a => !user.GetHideCategories().Contains(a.TagName));

            #region Search_TitlesOnly

            switch (flags)
            {
                case SearchFlags.Titles:
                    return SearchTitlesIndexOf(query, limit, allSeries);
                case SearchFlags.Fuzzy | SearchFlags.Titles:
                    return SearchTitlesFuzzy(query, limit, allSeries);
                case SearchFlags.Tags:
                    return SearchTagsEquals(query, limit, allTags);
                case SearchFlags.Fuzzy | SearchFlags.Tags:
                    return SearchTagsFuzzy(query, limit, allTags);
                case SearchFlags.Tags | SearchFlags.Titles:
                    List<SearchResult> titleResult = SearchTitlesIndexOf(query, limit, allSeries);

                    int tagLimit = limit - titleResult.Count;
                    if (tagLimit <= 0) return titleResult;
                    titleResult.AddRange(SearchTagsEquals(query, tagLimit, allTags));
                    return titleResult;
                case SearchFlags.Fuzzy | SearchFlags.Tags | SearchFlags.Titles:
                    List<SearchResult> titles = SearchTitlesFuzzy(query, limit, allSeries);

                    int tagLimit2 = limit - titles.Count;
                    if (tagLimit2 <= 0) return titles;
                    titles.AddRange(SearchTagsFuzzy(query, tagLimit2, allTags));
                    return titles;
            }

            #endregion

            return new List<SearchResult>();
        }

        private static List<SearchResult> SearchTagsEquals(string query, int limit, ParallelQuery<AniDB_Tag> allTags)
        {
            List<SearchResult> series = new List<SearchResult>();
            CustomTag customTag = RepoFactory.CustomTag.GetAll()
                .FirstOrDefault(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase));
            if (customTag != null)
                series.AddRange(from xref in RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTag.CustomTagID)
                    where xref.CrossRefType == (int) CustomTagCrossRefType.Anime
                    let anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID)
                    orderby anime.GetSeriesName()
                    select new SearchResult
                    {
                        Distance = 0,
                        Index = 0,
                        Match = customTag.TagName,
                        Result = anime,
                        ExactMatch = true
                    });

            // due to exact match, only one is needed
            AniDB_Tag tag =
                allTags.FirstOrDefault(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase));
            if (tag == null) return series.Take(limit).ToList();
            List<AniDB_Anime_Tag> xrefs = RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID);
            series.AddRange(from xref in xrefs
                let anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID)
                orderby xref.Weight descending, anime.GetSeriesName()
                select new SearchResult
                {
                    Distance = (600 - xref.Weight) / 600D,
                    Index = 0,
                    Match = tag.TagName,
                    Result = anime,
                    ExactMatch = true
                });
            return series.Take(limit).ToList();
        }

        private static List<SearchResult> SearchTitlesIndexOf(string query, int limit,
            ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            string sanitizedQuery = SanitizeFuzzy(query, false);
            return allSeries.GroupBy(a => a.AnimeGroupID).Select(a => CheckTitlesIndexOf(a, sanitizedQuery))
                .Where(a => a != null).OrderBy(a => a.Index).SelectMany(a => a.Results.Select(b => new SearchResult
                {
                    Distance = a.Distance,
                    Index = a.Index,
                    ExactMatch = a.ExactMatch,
                    Match = a.Match,
                    Result = b
                })).Take(limit).ToList();
        }

        private static List<SearchResult> SearchTitlesFuzzy(string query, int limit,
            ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            return allSeries.GroupBy(a => a.AnimeGroupID).Select(a => CheckTitlesFuzzy(a, query)).Where(a => a != null)
                .OrderBy(a => a.Index).ThenBy(a => a.Distance).SelectMany(a => a.Results.Select(b => new SearchResult
                {
                    Distance = a.Distance,
                    Index = a.Index,
                    ExactMatch = a.ExactMatch,
                    Match = a.Match,
                    Result = b
                })).Take(limit).ToList();
        }

        private static List<SearchResult> SearchTagsFuzzy(string query, int limit, ParallelQuery<AniDB_Tag> allTags)
        {
            List<SearchResult> series = new List<SearchResult>();
            IEnumerable<CustomTag> customTags = RepoFactory.CustomTag.GetAll()
                .Where(a => a.TagName.FuzzyMatches(query));
            series.AddRange(from customTag in customTags
                from xref in RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTag.CustomTagID)
                where xref.CrossRefType == (int) CustomTagCrossRefType.Anime
                let anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID)
                orderby anime.GetSeriesName()
                select new SearchResult
                {
                    Distance = 0,
                    Index = 0,
                    Match = customTag.TagName,
                    Result = anime,
                    ExactMatch = true
                });

            series.AddRange(from tag in allTags.Where(a => a.TagName.FuzzyMatches(query))
                from xref in RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID)
                let anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID)
                orderby xref.Weight descending, anime.GetSeriesName()
                select new SearchResult
                {
                    Distance = (600 - xref.Weight) / 600D,
                    Index = 0,
                    Match = tag.TagName,
                    Result = anime,
                    ExactMatch = true
                });
            return series.Take(limit).ToList();
        }

        public abstract class BaseSearchItem
        {
            public bool ExactMatch { get; set; }
            public double Distance { get; set; }
            public int Index { get; set; }
            public string Match { get; set; }
        }

        public class SearchGrouping : BaseSearchItem
        {
            public List<SVR_AnimeSeries> Results { get; set; }
        }

        public class SearchResult : BaseSearchItem
        {
            public SVR_AnimeSeries Result { get; set; }
        }
    }
}