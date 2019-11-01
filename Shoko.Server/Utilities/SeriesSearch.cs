using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Utilities
{
    public static class SeriesSearch
    {
        /// <summary>
        /// Join a string like string.Join but
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="values"></param>
        /// <param name="replaceInvalid"></param>
        /// <returns></returns>
        private static string Join(string separator, IEnumerable<string> values, bool replaceInvalid)
        {
            if (!replaceInvalid) return string.Join(separator, values);

            List<string> newItems = values.Select(s => SanitizeFuzzy(s, true)).ToList();

            return string.Join(separator, newItems);
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
        /// function used in fuzzy search
        /// </summary>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        private static void CheckTitlesFuzzy(SVR_AnimeSeries a, string query,
            ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return;
            var dist = new Misc.SearchInfo<string> {Index = -1, Distance = int.MaxValue};
            string match = string.Empty;
            foreach (string title in a.GetAllTitles())
            {
                if (string.IsNullOrEmpty(title)) continue;
                int k = Math.Max(Math.Min((int) (title.Length / 6D), (int) (query.Length / 6D)), 1);
                if (query.Length <= 4 || title.Length <= 4) k = 0;
                var result = Misc.DiceFuzzySearch(title, query, k, title);
                if (result.Index == -1) continue;
                if (result.Distance < dist.Distance)
                {
                    dist = result;
                }
                else if (Math.Abs(result.Distance - dist.Distance) < 0.0001D)
                {
                    if (title.Length < match.Length) match = title;
                }
            }

            // Keep the lowest distance, then by shortest title
            if (dist.Distance < int.MaxValue)
                distLevenshtein.AddOrUpdate(a, dist,
                    (key, oldValue) =>
                    {
                        if (oldValue.Distance < dist.Distance) return oldValue;
                        if (Math.Abs(oldValue.Distance - dist.Distance) < 0.0001D)
                            return oldValue.Result.Length < dist.Result.Length
                                ? oldValue
                                : dist;

                        return dist;
                    });
        }

        /// <summary>
        /// function used in fuzzy tag search
        /// </summary>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        private static void CheckTagsFuzzy(SVR_AnimeSeries a, string query,
            ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<AniDB_Tag>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            Misc.SearchInfo<AniDB_Tag> dist = new Misc.SearchInfo<AniDB_Tag> {Index = -1, Distance = int.MaxValue};
            if (a?.Contract?.AniDBAnime?.Tags != null &&
                a.Contract.AniDBAnime.Tags.Count > 0)
            {
                foreach (AniDB_Tag tag in a.GetAnime().GetTags())
                {
                    if (string.IsNullOrEmpty(tag?.TagName)) continue;
                    int k = Math.Min((int) (tag.TagName.Length / 6D), (int) (query.Length / 6D));
                    var result = Misc.DiceFuzzySearch(tag.TagName, query, k, tag);
                    if (result.Index == -1) continue;
                    if (result.Distance < dist.Distance)
                    {
                        dist = result;
                    }
                }

                if (dist.Distance < int.MaxValue)
                    distLevenshtein.AddOrUpdate(a, dist,
                        (key, oldValue) =>
                            Math.Abs(Math.Min(oldValue.Distance, dist.Distance) - dist.Distance) < 0.0001D ? dist : oldValue);
            }

            if (distLevenshtein.Count >= limit || a?.Contract?.AniDBAnime?.CustomTags == null ||
                a.Contract.AniDBAnime.CustomTags.Count <= 0) return;

            dist = new Misc.SearchInfo<AniDB_Tag> {Index = -1, Distance = int.MaxValue};
            foreach (CustomTag customTag in a.GetAnime().GetCustomTagsForAnime())
            {
                if (string.IsNullOrEmpty(customTag?.TagName)) continue;
                int k = Math.Min((int) (customTag.TagName.Length / 6D), (int) (query.Length / 6D));
                var result = Misc.DiceFuzzySearch<CustomTag>(customTag.TagName, query, k, customTag);
                if (result.Index == -1) continue;
                if (result.Distance < dist.Distance)
                {
                    dist = new Misc.SearchInfo<AniDB_Tag>
                    {
                        Distance = result.Distance,
                        Index = result.Index,
                        ExactMatch = result.ExactMatch,
                        Result = new AniDB_Tag
                        {
                            TagName = result.Result.TagName
                        }
                    };
                }
            }

            if (dist.Distance < int.MaxValue)
                distLevenshtein.AddOrUpdate(a, dist,
                    (key, oldValue) => Math.Abs(Math.Min(oldValue.Distance, dist.Distance) - dist.Distance) < 0.0001D
                        ? dist : oldValue);
        }

        /// <summary>
        /// Search for series with given query in name or tag
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="userID">user id</param>
        /// <param name="limit">The number of results to return</param>
        /// <param name="flags" >The SearchFlags to determine the type of search</param>
        /// <returns><see cref="List{SearchResult}"/></returns>
        public static List<SearchResult> Search(int userID, string query, int limit, SearchFlags flags)
        {
            query = query.ToLowerInvariant();

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) throw new Exception("User not found");
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll().Where(a =>
                    a?.GetAnime() != null && !a.GetAnime().GetAllTags().FindInEnumerable(user.GetHideCategories()))
                .AsParallel();

            #region Search_TitlesOnly

            switch (flags)
            {
                case SearchFlags.Titles:
                    return SearchTitlesIndexOf(query, limit, allSeries);
                case SearchFlags.Fuzzy | SearchFlags.Titles:
                    return SearchTitlesFuzzy(query, limit, allSeries);
                case SearchFlags.Tags:
                    return SearchTagsEquals(query, limit, allSeries);
                case SearchFlags.Fuzzy | SearchFlags.Tags:
                    return SearchTagsFuzzy(query, limit, allSeries);
                case SearchFlags.Tags | SearchFlags.Titles:
                    var titleResult = SearchTitlesIndexOf(query, limit, allSeries);

                    int tagLimit = limit - titleResult.Count;
                    if (tagLimit <= 0) return titleResult;
                    titleResult.AddRange(SearchTagsEquals(query, tagLimit, allSeries));
                    return titleResult;
                case SearchFlags.Fuzzy | SearchFlags.Tags | SearchFlags.Titles:
                    var titles = SearchTitlesFuzzy(query, limit, allSeries);

                    int tagLimit2 = limit - titles.Count;
                    if (tagLimit2 <= 0) return titles;
                    titles.AddRange(SearchTagsEquals(query, tagLimit2, allSeries));
                    return titles;
            }

            #endregion

            return new List<SearchResult>();
        }

        private static List<SearchResult> SearchTagsEquals(string query, int limit, ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            return allSeries.Select(series =>
                {
                    foreach (CustomTag customTag in series.GetAnime().GetCustomTagsForAnime())
                    {
                        if (!customTag.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase)) continue;

                        return (true, new SearchResult
                        {
                            Result = series,
                            ExactMatch = true,
                            Match = customTag.TagName
                        });
                    }

                    foreach (AniDB_Anime_Tag animeTag in series.GetAnime().GetAnimeTags())
                    {
                        var tag = RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID);
                        if (tag == null) continue;
                        if (!tag.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase)) continue;
                        return (false, new SearchResult
                        {
                            Result = series,
                            ExactMatch = true,
                            Match = tag.TagName,
                            // 600 is a value pulled from the wiki and database
                            // according to wiki, there are 6 levels, in db, they are by 100
                            Distance = 600 - animeTag.Weight
                        });
                    }

                    return (false, null);
                }).Where(result => result.Item2 != null).OrderBy(result => result.Item1)
                .ThenBy(result => result.Item2.Result.GetSeriesName()).Select(result => result.Item2).Take(limit)
                .ToList();
        }

        private static List<SearchResult> SearchTitlesIndexOf(string query, int limit, ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            string sanitizedQuery = SanitizeFuzzy(query, false);
            return allSeries.Select(series1 =>
            {
                foreach (string title in series1.GetAllTitles())
                {
                    int index = title.IndexOf(sanitizedQuery, StringComparison.InvariantCultureIgnoreCase);
                    if (index == -1) continue;
                    return new SearchResult
                    {
                        Result = series1,
                        Distance = 0,
                        ExactMatch = true,
                        Index = index,
                        Match = sanitizedQuery
                    };
                }

                return null;
            }).Where(a => a != null).OrderBy(a => a.Index).ThenBy(a => a.Result.GetSeriesName()).Take(limit).ToList();
        }

        private static List<SearchResult> SearchTitlesFuzzy(string query, int limit, ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<string>>();
            allSeries.ForAll(a => CheckTitlesFuzzy(a, query, distLevenshtein, limit));

            IEnumerable<SearchGrouping> tempListToSort = distLevenshtein.Keys.GroupBy(a => a.AnimeGroupID)
                .Select(a => SelectGroupings(a, distLevenshtein));

            return tempListToSort.OrderBy(a => a.Distance)
                .SelectMany(a => a.Results.Select(result => new SearchResult
                        {
                            Result = result,
                            Distance = a.Distance,
                            Index = a.Index,
                            Match = a.Match,
                            ExactMatch = a.ExactMatch
                        })).ToList();
        }
        
        private static List<SearchResult> SearchTagsFuzzy(string query, int limit, ParallelQuery<SVR_AnimeSeries> allSeries)
        {
            ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<AniDB_Tag>> distLevenshtein =
                new ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<AniDB_Tag>>();
            allSeries.ForAll(a => CheckTagsFuzzy(a, query, distLevenshtein, limit));

            return distLevenshtein.OrderBy(a => a.Value.Distance)
                .ThenBy(a =>
                {
                    // Sort by Weight
                    // if result == null, then it's a custom tag
                    if (a.Value.Result == null || a.Value.Result?.TagID == 0) return 0;
                    var animeTag =
                        RepoFactory.AniDB_Anime_Tag.GetByAnimeIDAndTagID(a.Key.AniDB_ID, a.Value.Result.TagID);
                    if (animeTag == null) return 600;
                    return 600 - animeTag.Weight;
                })
                .ThenBy(a => a.Value.Result?.TagName)
                .ThenBy(a => a.Key.GetSeriesName())
                .Select(a => new SearchResult
                {
                    Result = a.Key,
                    Distance = a.Value.Distance,
                    Index = a.Value.Index,
                    ExactMatch = a.Value.ExactMatch,
                    Match = a.Value.Result?.TagName
                }).ToList();
        }

        private static SearchGrouping SelectGroupings(IGrouping<int, SVR_AnimeSeries> a,
            ConcurrentDictionary<SVR_AnimeSeries, Misc.SearchInfo<string>> distLevenshtein)
        {
            var tempSeries = a.ToList();
            tempSeries.Sort((j, k) =>
            {
                var result1 = distLevenshtein[j];
                var result2 = distLevenshtein[k];
                var exactMatch = result2.ExactMatch.CompareTo(result1.ExactMatch);
                if (exactMatch != 0) return exactMatch;
                // calculate word boundaries
                // we already know that they are equal to each other here
                if (result1.ExactMatch && result2.ExactMatch)
                {
                    bool startsWith1 = result1.Index == 0;
                    if (!startsWith1)
                    {
                        char startChar1 = result1.Result[result1.Index - 1];
                        startsWith1 = char.IsWhiteSpace(startChar1) || char.IsPunctuation(startChar1) ||
                                      char.IsSeparator(startChar1);
                    }

                    bool startsWith2 = result2.Index == 0;
                    if (!startsWith2)
                    {
                        char startChar2 = result2.Result[result2.Index - 1];
                        startsWith2 = char.IsWhiteSpace(startChar2) || char.IsPunctuation(startChar2) ||
                                      char.IsSeparator(startChar2);
                    }

                    int index1 = result1.Result.Length + result1.Index;
                    bool endsWith1 = result1.Result.Length <= index1;
                    if (!endsWith1)
                    {
                        char endChar1 = result1.Result[index1];
                        endsWith1 = char.IsWhiteSpace(endChar1) || char.IsPunctuation(endChar1) ||
                                    char.IsSeparator(endChar1);
                    }

                    int index2 = result2.Result.Length + result2.Index;
                    bool endsWith2 = result2.Result.Length <= index2;
                    if (!endsWith2)
                    {
                        char endChar2 = result2.Result[index2];
                        endsWith2 = char.IsWhiteSpace(endChar2) || char.IsPunctuation(endChar2) ||
                                    char.IsSeparator(endChar2);
                    }

                    int word = (startsWith2 && endsWith2).CompareTo(startsWith1 && endsWith1);
                    if (word != 0) return word;
                    int indexComp = result1.Index.CompareTo(result2.Index);
                    if (indexComp != 0) return indexComp;
                }

                var distance = result1.Distance.CompareTo(result2.Distance);
                if (distance != 0) return distance;
                string title1 = j.GetSeriesName();
                string title2 = k.GetSeriesName();
                if (title1 == null && title2 == null) return 0;
                if (title1 == null) return 1;
                if (title2 == null) return -1;
                return String.Compare(title1, title2, StringComparison.InvariantCultureIgnoreCase);
            });
            var result = new SearchGrouping
            {
                Results = a.OrderBy(b => b.AirDate).ToList(),
                ExactMatch = distLevenshtein[tempSeries[0]].ExactMatch,
                Distance = distLevenshtein[tempSeries[0]].Distance,
                Index = distLevenshtein[tempSeries[0]].Index,
                Match = distLevenshtein[tempSeries[0]].Result
            };
            return result;
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

        [Flags]
        public enum SearchFlags
        {
            Tags = 1,
            Titles = 2,
            Fuzzy = 4
        }
    }
}