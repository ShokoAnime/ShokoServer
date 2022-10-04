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

namespace Shoko.Server.Utilities;

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
        if (!replaceInvalid)
        {
            return value;
        }

        value = value.FilterCharacters(InvalidPathChars, true);
        value = ReplaceWithSpace.Aggregate(value, (current, c) => current.Replace(c, ' '));

        return value.CompactWhitespaces();
    }

    /// <summary>
    ///     function used in fuzzy search
    /// </summary>
    /// <param name="grouping"></param>
    /// <param name="query"></param>
    private static SearchResult<List<SVR_AnimeSeries>> CheckTitlesFuzzy(IGrouping<int, SVR_AnimeSeries> grouping,
        string query)
    {
        if (!(grouping?.SelectMany(a => a.GetAllTitles()).Any() ?? false))
        {
            return null;
        }

        SearchResult<List<SVR_AnimeSeries>> dist = null;

        foreach (var item in grouping)
        {
            foreach (var title in item.GetAllTitles())
            {
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var k = Math.Max(Math.Min((int)(title.Length / 6D), (int)(query.Length / 6D)), 1);
                if (query.Length <= 4 || title.Length <= 4)
                {
                    k = 0;
                }

                var result =
                    Misc.DiceFuzzySearch(title, query, k, grouping);
                if (result.Index == -1)
                {
                    continue;
                }

                var searchGrouping = new SearchResult<List<SVR_AnimeSeries>>
                {
                    Distance = result.Distance,
                    Index = result.Index,
                    ExactMatch = result.ExactMatch,
                    Match = title,
                    Result = grouping.OrderBy(a => a.AirDate).ToList()
                };
                if (result.Distance < (dist?.Distance ?? int.MaxValue))
                {
                    dist = searchGrouping;
                }
            }
        }

        return dist;
    }

    /// <summary>
    ///     function used in fuzzy search
    /// </summary>
    /// <param name="grouping"></param>
    /// <param name="query"></param>
    private static SearchResult<List<SVR_AnimeSeries>> CheckTitlesIndexOf(IGrouping<int, SVR_AnimeSeries> grouping,
        string query)
    {
        if (!(grouping?.SelectMany(a => a.GetAllTitles()).Any() ?? false))
        {
            return null;
        }

        SearchResult<List<SVR_AnimeSeries>> dist = null;

        foreach (var item in grouping)
        foreach (var title in item.GetAllTitles())
        {
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            var result = title.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (result == -1)
            {
                continue;
            }

            var searchGrouping = new SearchResult<List<SVR_AnimeSeries>>
            {
                Distance = 0,
                Index = result,
                ExactMatch = true,
                Match = title,
                Result = grouping.OrderBy(a => a.AirDate).ToList()
            };
            if (result < (dist?.Index ?? int.MaxValue))
            {
                dist = searchGrouping;
            }
        }

        return dist;
    }

    public static List<SearchResult<T>> SearchCollection<T>(string query, IEnumerable<T> list,
        Func<T, List<string>> selector)
    {
        var parallelList = list.ToList().AsParallel();
        var results = parallelList.Select(a =>
        {
            var titles = selector(a);
            SearchResult<T> dist = null;
            foreach (var title in titles)
            {
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var k = Math.Max(Math.Min((int)(title.Length / 6D), (int)(query.Length / 6D)), 1);
                if (query.Length <= 4 || title.Length <= 4)
                {
                    k = 0;
                }

                var result =
                    Misc.DiceFuzzySearch(title, query, k, a);
                if (result.Index == -1)
                {
                    continue;
                }

                var searchGrouping = new SearchResult<T>
                {
                    Distance = result.Distance,
                    Index = result.Index,
                    ExactMatch = result.ExactMatch,
                    Match = title,
                    Result = a
                };
                if (result.Distance < (dist?.Distance ?? int.MaxValue))
                {
                    dist = searchGrouping;
                }
            }

            return dist;
        }).Where(a => a != null && a.Index != -1).ToList().OrderBy(a => a.Index).ThenBy(a => a.Distance).ToList();

        return results;
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
    public static List<SearchResult<SVR_AnimeSeries>> Search(int userID, string query, int limit, SearchFlags flags,
        TagFilter.Filter tagFilter = TagFilter.Filter.None)
    {
        query = query.ToLowerInvariant();

        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        var allSeries =
            RepoFactory.AnimeSeries.GetAll().AsParallel().Where(a =>
                a?.GetAnime() != null && (a.GetAnime().GetAllTags().Count == 0 ||
                                          !a.GetAnime().GetAllTags().FindInEnumerable(user.GetHideCategories())));

        var allTags = RepoFactory.AniDB_Tag.GetAll().AsParallel()
            .Where(a =>
            {
                return !user.GetHideCategories().Contains(a.TagName) &&
                       !TagFilter.IsTagBlackListed(a.TagName, tagFilter);
            });

        //search by anime id
        if (int.TryParse(query, out var aid))
        {
            var aidResults = SearchTitlesByAnimeID(aid, allSeries);
            if (aidResults.Count > 0)
            {
                return aidResults;
            }
        }

        #region Search_TitlesOnly

        switch (flags)
        {
            case SearchFlags.Titles:
                return SearchTitlesIndexOf(query, limit, allSeries);
            case SearchFlags.Fuzzy | SearchFlags.Titles:
                return SearchTitlesFuzzy(query, limit, allSeries);
            case SearchFlags.Tags:
                return SearchTagsEquals(query, limit, user, allTags);
            case SearchFlags.Fuzzy | SearchFlags.Tags:
                return SearchTagsFuzzy(query, limit, user, allTags);
            case SearchFlags.Tags | SearchFlags.Titles:
                var titleResult = SearchTitlesIndexOf(query, limit, allSeries);

                var tagLimit = limit - titleResult.Count;
                if (tagLimit <= 0)
                {
                    return titleResult;
                }

                titleResult.AddRange(SearchTagsEquals(query, tagLimit, user, allTags));
                return titleResult;
            case SearchFlags.Fuzzy | SearchFlags.Tags | SearchFlags.Titles:
                var titles = SearchTitlesFuzzy(query, limit, allSeries);

                var tagLimit2 = limit - titles.Count;
                if (tagLimit2 <= 0)
                {
                    return titles;
                }

                titles.AddRange(SearchTagsFuzzy(query, tagLimit2, user, allTags));
                return titles;
        }

        #endregion

        return new List<SearchResult<SVR_AnimeSeries>>();
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTitlesByAnimeID(int aid,
        ParallelQuery<SVR_AnimeSeries> allSeries)
    {
        // We should only have one, but meh
        return allSeries
            .Where(a => a.AniDB_ID == aid).Select(b => new SearchResult<SVR_AnimeSeries>
            {
                Distance = 0,
                Index = 0,
                ExactMatch = true,
                Match = b.AniDB_ID.ToString(),
                Result = b
            })
            .ToList();
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTagsEquals(string query, int limit, SVR_JMMUser user,
        ParallelQuery<AniDB_Tag> allTags)
    {
        var series = new List<SearchResult<SVR_AnimeSeries>>();
        IEnumerable<CustomTag> customTags = RepoFactory.CustomTag.GetAll();
        series.AddRange(customTags.Where(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag =>
            {
                return RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID)
                    .Select(xref =>
                    {
                        if (xref.CrossRefType != (int)CustomTagCrossRefType.Anime)
                        {
                            return null;
                        }

                        var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                        // Because we are searching tags, then getting series from it, we need to make sure it's allowed
                        // for example, porn could have the drugs tag, even though it's not a "porn tag"
                        if (anime?.GetAnime()?.GetAllTags().FindInEnumerable(user.GetHideCategories()) ?? true)
                        {
                            return null;
                        }

                        return new SearchResult<SVR_AnimeSeries>
                        {
                            Distance = 0,
                            Index = 0,
                            Match = tag.TagName,
                            Result = anime,
                            ExactMatch = true
                        };
                    }).Where(a => a != null).OrderBy(a => a.Distance).ThenBy(a => a.Result.GetSeriesName());
            }).Take(limit));

        limit -= series.Count;

        series.AddRange(allTags.Where(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag =>
            {
                return RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID)
                    .Select(xref =>
                    {
                        var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                        // Because we are searching tags, then getting series from it, we need to make sure it's allowed
                        // for example, porn could have the drugs tag, even though it's not a "porn tag"
                        if (anime?.GetAnime()?.GetAllTags().FindInEnumerable(user.GetHideCategories()) ?? true)
                        {
                            return null;
                        }

                        return new SearchResult<SVR_AnimeSeries>
                        {
                            Distance = (600 - xref.Weight) / 600D,
                            Index = 0,
                            Match = tag.TagName,
                            Result = anime,
                            ExactMatch = true
                        };
                    }).Where(a => a != null).OrderBy(a => a.Distance).ThenBy(a => a.Result.GetSeriesName());
            }).Take(limit));
        return series;
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTagsFuzzy(string query, int limit, SVR_JMMUser user,
        ParallelQuery<AniDB_Tag> allTags)
    {
        var series = new List<SearchResult<SVR_AnimeSeries>>();
        IEnumerable<Misc.SearchInfo<CustomTag>> customTags = RepoFactory.CustomTag.GetAll().Select(a =>
        {
            if (user.GetHideCategories().Contains(a.TagName))
            {
                return null;
            }

            var tag = Misc.DiceFuzzySearch(a.TagName, query, 0, a);
            if (tag.Index == -1 || tag.Result == null)
            {
                return null;
            }

            return tag;
        }).Where(a => a != null).OrderBy(a => a.Distance);
        series.AddRange(customTags.SelectMany(tag =>
        {
            return RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.Result.CustomTagID)
                .Select(xref =>
                {
                    if (xref.CrossRefType != (int)CustomTagCrossRefType.Anime)
                    {
                        return null;
                    }

                    var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                    // Because we are searching tags, then getting series from it, we need to make sure it's allowed
                    // for example, porn could have the drugs tag, even though it's not a "porn tag"
                    if (anime?.GetAnime()?.GetAllTags().FindInEnumerable(user.GetHideCategories()) ?? true)
                    {
                        return null;
                    }

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = tag.Distance,
                        Index = tag.Index,
                        Match = tag.Result.TagName,
                        Result = anime,
                        ExactMatch = tag.ExactMatch
                    };
                }).Where(b => b != null).OrderBy(b => b.Distance).ThenBy(b => b.Result.GetSeriesName()).ToList();
        }).Take(limit));

        limit -= series.Count;

        var tags = allTags.Select(tag =>
        {
            var result = Misc.DiceFuzzySearch(tag.TagName, query, 0, tag);
            if (result.Index == -1 || result.Result == null)
            {
                return null;
            }

            return result;
        }).Where(a => a != null).OrderBy(a => a.Distance);
        series.AddRange(tags.SelectMany(tag =>
        {
            return RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.Result.TagID)
                .Select(xref =>
                {
                    var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    // Because we are searching tags, then getting series from it, we need to make sure it's allowed
                    // for example, porn could have the drugs tag, even though it's not a "porn tag"
                    if (anime?.GetAnime()?.GetAllTags().FindInEnumerable(user.GetHideCategories()) ?? true)
                    {
                        return null;
                    }

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = (600D - xref.Weight) / 600,
                        Index = tag.Index,
                        Match = tag.Result.TagName,
                        Result = anime,
                        ExactMatch = tag.ExactMatch
                    };
                }).Where(a => a != null).OrderBy(a => a.Distance).ThenBy(a => a.Result.GetSeriesName()).ToList();
        }).Take(limit));
        return series;
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTitlesIndexOf(string query, int limit,
        ParallelQuery<SVR_AnimeSeries> allSeries)
    {
        var sanitizedQuery = SanitizeFuzzy(query, false);
        return allSeries.GroupBy(a => a.AnimeGroupID).Select(a => CheckTitlesIndexOf(a, sanitizedQuery))
            .Where(a => a != null).OrderBy(a => a.Index).SelectMany(a => a.Result.Select(b =>
                new SearchResult<SVR_AnimeSeries>
                {
                    Distance = a.Distance,
                    Index = a.Index,
                    ExactMatch = a.ExactMatch,
                    Match = a.Match,
                    Result = b
                })).Take(limit).ToList();
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTitlesFuzzy(string query, int limit,
        ParallelQuery<SVR_AnimeSeries> allSeries)
    {
        // ToList() after the Parallel compatible operations are done to prevent an OutOfMemoryException on the ParallelEnumerable
        return allSeries.GroupBy(a => a.AnimeGroupID).Select(a => CheckTitlesFuzzy(a, query)).Where(a => a != null)
            .ToList()
            .OrderBy(a => a.Index).ThenBy(a => a.Distance).SelectMany(a => a.Result.Select(b =>
                new SearchResult<SVR_AnimeSeries>
                {
                    Distance = a.Distance,
                    Index = a.Index,
                    ExactMatch = a.ExactMatch,
                    Match = a.Match,
                    Result = b
                })).Take(limit).ToList();
    }

    public class SearchResult<T>
    {
        public bool ExactMatch { get; set; }
        public double Distance { get; set; }
        public int Index { get; set; }
        public string Match { get; set; }

        public T Result { get; set; }
    }
}
