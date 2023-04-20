using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
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

    private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Concat(new char[] { '(', ')', '+' }).ToArray();

    private static readonly char[] ReplaceWithSpace = new char[] { '[', '-', '.', ']' };

    public static string SanitizeFuzzy(string value, bool replaceInvalid)
    {
        if (!replaceInvalid)
            return value;

        value = value.FilterCharacters(InvalidPathChars, true);
        value = ReplaceWithSpace.Aggregate(value, (current, c) => current.Replace(c, ' '));

        return value.CompactWhitespaces();
    }

    private static readonly IStringDistance DiceSearch = new SorensenDice();

    public static SearchResult<T> DiceFuzzySearch<T>(string text, string pattern, T value)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(pattern))
            return new() { Index = -1, Distance = int.MaxValue };
        // This forces ASCII, because it's faster to stop caring if ss and ß are the same
        // No it's not perfect, but it works better for those who just want to do lazy searching
        string inputString = text.FilterSearchCharacters();
        string query = pattern.FilterSearchCharacters();
        inputString = inputString.Replace('_', ' ').Replace('-', ' ');
        query = query.Replace('_', ' ').Replace('-', ' ');
        query = query.CompactWhitespaces();
        inputString = inputString.CompactWhitespaces();
        // Case insensitive. We just removed the fancy characters, so latin alphabet lowercase is all we should have
        query = query.ToLowerInvariant();
        inputString = inputString.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(inputString))
            return new() { Index = -1, Distance = int.MaxValue };

        // Shortcut
        int index = inputString.IndexOf(query, StringComparison.Ordinal);
        if (index > -1)
            return new() { Index = index, Distance = 0, ExactMatch = true, Result = value };

        // always search the longer string for the shorter one
        if (query.Length > inputString.Length)
        {
            string temp = query;
            query = inputString;
            inputString = temp;
        }

        double result = DiceSearch.Distance(inputString, query);

        // Don't count an error as liberally when the title is short
        if (inputString.Length < 5 && result > 0.5)
            return new() { Index = -1, Distance = int.MaxValue };


        if (result >= 0.8)
            return new() { Index = -1, Distance = int.MaxValue };

        return new() { Index = 0, Distance = result, Result = value };
    }

    public static bool FuzzyMatch(this string text, string query)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
            return false;
        var result = DiceFuzzySearch(text, query, text);
        if (result.ExactMatch)
            return true;
        if (text.Length <= 5 && result.Distance > 0.5D)
            return false;
        return result.Distance < 0.8D;
    }

    public static IOrderedEnumerable<SearchResult<T>> Search<T>(this IEnumerable<T> enumerable, string query, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
        => (SearchCollection(enumerable.AsParallel(), query, selector, fuzzy, take, skip) as IEnumerable<SearchResult<T>>)
            .OrderBy(a => a.Index)
            .ThenBy(a => a.Distance);

    public static OrderedParallelQuery<SearchResult<T>> Search<T>(this ParallelQuery<T> enumerable, string query, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
        => SearchCollection(enumerable, query, selector, fuzzy, take, skip)
            .OrderBy(a => a.Index)
            .ThenBy(a => a.Distance);

    private static ParallelQuery<SearchResult<T>> SearchCollection<T>(ParallelQuery<T> query, string search, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
    {
        // Don't do anything if we want to take zero or less entries.
        if (take.HasValue && take.Value <= 0)
            return new List<SearchResult<T>>().AsParallel();

        var enumerable = query
            .Select(t => selector(t)
                .Aggregate<string, SearchResult<T>>(null, (current, title) =>
                {
                    if (string.IsNullOrWhiteSpace(title))
                        return current;

                    if (fuzzy)
                    {
                        var result = DiceFuzzySearch(title, search, t);
                        if (result.Index == -1 || result.Distance >= (current?.Distance ?? int.MaxValue))
                            return current;

                        return result;
                    }

                    var index = title.IndexOf(search, StringComparison.OrdinalIgnoreCase);
                   if (index == -1 || index >= (current?.Index ?? int.MaxValue))
                        return current;

                    return new()
                    {
                        Distance = 0,
                        Index = index,
                        ExactMatch = true,
                        Match = title,
                        Result = t,
                    };

                })
            )
            .Where(a => a != null && a.Index != -1);

        if (skip.HasValue && skip.Value > 0)
            enumerable = enumerable.Skip(skip.Value);

        if (take.HasValue)
            enumerable = enumerable.Take(take.Value);

        return enumerable;
    }

    /// <summary>
    ///     Search for series with given query in name or tag
    /// </summary>
    /// <param name="query">target string</param>
    /// <param name="user">user</param>
    /// <param name="limit">The number of results to return</param>
    /// <param name="flags">The SearchFlags to determine the type of search</param>
    /// <param name="tagFilter">Tag filter to use if <paramref name="flags"/>
    /// includes <see cref="SearchFlags.Tags"/>.</param>
    /// <returns>
    ///     <see cref="List{SearchResult}" />
    /// </returns>
    public static List<SearchResult<SVR_AnimeSeries>> SearchSeries(SVR_JMMUser user, string query, int limit, SearchFlags flags,
            TagFilter.Filter tagFilter = TagFilter.Filter.None)
    {
        if (string.IsNullOrWhiteSpace(query) || user == null)
            return new List<SearchResult<SVR_AnimeSeries>>();

        query = query.ToLowerInvariant();
        var forbiddenTags = user.GetHideCategories();

        //search by anime id
        if (int.TryParse(query, out int animeID))
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            var anime = series.GetAnime();
            var tags = anime?.GetAllTags();
            if (anime != null && !tags.FindInEnumerable(forbiddenTags))
                return new List<SearchResult<SVR_AnimeSeries>>
                {
                    new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = 0,
                        Index = 0,
                        ExactMatch = true,
                        Match = series.AniDB_ID.ToString(),
                        Result = series,
                    },
                };
        }

        var allSeries = !flags.HasFlag(SearchFlags.Titles) ? null : RepoFactory.AnimeSeries.GetAll()
            .AsParallel()
            .Where(series =>
            {
                var anime = series.GetAnime();
                var tags = anime?.GetAllTags();
                return anime != null && (tags.Count == 0 || tags.FindInEnumerable(forbiddenTags));
            });
        var allTags = !flags.HasFlag(SearchFlags.Tags) ? null : RepoFactory.AniDB_Tag.GetAll()
            .AsParallel()
            .Where(tag => !forbiddenTags.Contains(tag.TagName) && !TagFilter.IsTagBlackListed(tag.TagName, tagFilter));
        return flags switch
        {
            SearchFlags.Titles => SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), false, limit)
                .OrderBy(a => a.Index)
                .ThenBy(a => a.Distance)
                .ToList(),
            SearchFlags.Fuzzy | SearchFlags.Titles => SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), true, limit)
                .OrderBy(a => a.Index)
                .ThenBy(a => a.Distance)
                .ToList(),
            SearchFlags.Tags => SearchTagsExact(query, limit, forbiddenTags, allTags),
            SearchFlags.Fuzzy | SearchFlags.Tags => SearchTagsFuzzy(query, limit, forbiddenTags, allTags),
            SearchFlags.Tags | SearchFlags.Titles => SearchTitleAndTags(query, limit, forbiddenTags, allSeries, allTags),
            SearchFlags.Fuzzy | SearchFlags.Tags | SearchFlags.Titles => SearchTitleAndTagsFuzzy(query, limit, forbiddenTags, allSeries, allTags),
            _ => new List<SearchResult<SVR_AnimeSeries>>(),
        };
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTitleAndTags(
        string query,
        int limit,
        HashSet<string> forbiddenTags,
        ParallelQuery<SVR_AnimeSeries> allSeries,
        ParallelQuery<AniDB_Tag> allTags)
    {
        var titleResult = SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), false, limit)
            .OrderBy(a => a.Index)
            .ThenBy(a => a.Distance)
            .ToList();
        var tagLimit = limit - titleResult.Count;
        if (tagLimit > 0)
            titleResult.AddRange(SearchTagsExact(query, tagLimit, forbiddenTags, allTags));
        return titleResult;
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTitleAndTagsFuzzy(
        string query,
        int limit,
        HashSet<string> forbiddenTags,
        ParallelQuery<SVR_AnimeSeries> allSeries,
        ParallelQuery<AniDB_Tag> allTags)
    {
        var titleResult = SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), true, limit)
            .OrderBy(a => a.Index)
            .ThenBy(a => a.Distance)
            .ToList();
        var tagLimit = limit - titleResult.Count;
        if (tagLimit > 0)
            titleResult.AddRange(SearchTagsFuzzy(query, tagLimit, forbiddenTags, allTags));
        return titleResult;
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTagsExact(string query, int limit,
        HashSet<string> forbiddenTags, ParallelQuery<AniDB_Tag> allTags)
    {
        var seriesList = new List<SearchResult<SVR_AnimeSeries>>();
        seriesList.AddRange(RepoFactory.CustomTag.GetAll()
            .Where(tag => tag.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID)
                .Select(xref =>
                {
                    if (xref.CrossRefType != (int)CustomTagCrossRefType.Anime)
                        return null;

                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                    var anime = series?.GetAnime();
                    var tags = anime?.GetAllTags();
                    if (anime == null || (tags.Count == 0 || tags.FindInEnumerable(forbiddenTags)))
                        return null;

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = 0,
                        Index = 0,
                        Match = tag.TagName,
                        Result = series,
                        ExactMatch = true
                    };
                })
                .Where(a => a != null)
            )
            .OrderBy(a => a.Result.GetSeriesName())
            .Take(limit)
        );

        limit -= seriesList.Count;

        seriesList.AddRange(allTags
            .Where(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag => RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    var anime = series?.GetAnime();
                    var tags = anime?.GetAllTags();
                    if (anime == null || (tags.Count == 0 || tags.FindInEnumerable(forbiddenTags)))
                        return null;

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = (600 - xref.Weight) / 600D,
                        Index = 0,
                        Match = tag.TagName,
                        Result = series,
                        ExactMatch = true
                    };
                })
                .Where(a => a != null)
            )
            .OrderBy(a => a.Distance)
            .ThenBy(a => a.Result.GetSeriesName())
            .Take(limit)
        );
        return seriesList;
    }

    private static List<SearchResult<SVR_AnimeSeries>> SearchTagsFuzzy(string query, int limit,
        HashSet<string> forbiddenTags, ParallelQuery<AniDB_Tag> allTags)
    {
        var seriesList = new List<SearchResult<SVR_AnimeSeries>>();
        var customTags = RepoFactory.CustomTag.GetAll()
            .AsParallel()
            .Select(tag =>
            {
                if (forbiddenTags.Contains(tag.TagName))
                    return null;

                var result = DiceFuzzySearch(tag.TagName, query, tag);
                if (result.Index == -1 || result.Result == null)
                    return null;

                return result;
            })
            .Where(a => a != null);
        seriesList.AddRange(customTags
            .SelectMany(tag => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.Result.CustomTagID)
                .Select(xref =>
                {
                    if (xref.CrossRefType != (int)CustomTagCrossRefType.Anime)
                        return null;

                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                    var anime = series?.GetAnime();
                    var tags = anime?.GetAllTags();
                    if (anime == null || (tags.Count == 0 || tags.FindInEnumerable(forbiddenTags)))
                        return null;

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = tag.Distance,
                        Index = tag.Index,
                        Match = tag.Result.TagName,
                        Result = series,
                        ExactMatch = tag.ExactMatch
                    };
                })
                .Where(b => b != null)
            )
            .OrderBy(b => b.Distance)
            .ThenBy(b => b.Result.GetSeriesName())
            .Take(limit));

        limit -= seriesList.Count;

        var tags = allTags
            .Select(tag =>
            {
                var result = DiceFuzzySearch(tag.TagName, query, tag);
                if (result.Index == -1 || result.Result == null)
                    return null;

                return result;
            })
            .Where(a => a != null);
        seriesList.AddRange(tags
            .SelectMany(tag => RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.Result.TagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    var anime = series?.GetAnime();
                    var tags = anime?.GetAllTags();
                    if (anime == null || (tags.Count == 0 || tags.FindInEnumerable(forbiddenTags)))
                        return null;

                    return new SearchResult<SVR_AnimeSeries>
                    {
                        Distance = (tag.Distance + (600 - xref.Weight) / 600D) / 2,
                        Index = tag.Index,
                        Match = tag.Result.TagName,
                        Result = series,
                        ExactMatch = tag.ExactMatch
                    };
                })
                .Where(a => a != null)
            )
            .OrderBy(a => a.Distance)
            .ThenBy(a => a.Result.GetSeriesName())
            .Take(limit)
        );
        return seriesList;
    }

    private static Func<SVR_AnimeSeries, IEnumerable<string>> CreateSeriesTitleDelegate()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var languages = new HashSet<string> { "en", "x-jat" };
        languages.UnionWith(settings.LanguagePreference);
        return series => RepoFactory.AniDB_Anime_Title.GetByAnimeID(series.AniDB_ID)
            .Where(title => title.TitleType == TitleType.Main || languages.Contains(title.LanguageCode))
            .Select(title => title.Title);
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
