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
            return new();
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
            return new();

        // Shortcut
        var lengthDiff = Math.Abs(query.Length - inputString.Length);
        int index = inputString.IndexOf(query, StringComparison.Ordinal);
        if (index > -1)
            return new()
            {
                ExactMatch = true,
                Index = index,
                LengthDifference = lengthDiff,
                Match = text,
                Result = value,
            };

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
            return new();

        if (result >= 0.8)
            return new();

        return new()
        {
            Distance = result,
            LengthDifference = lengthDiff,
            Match = text,
            Result = value,
        };
    }

    private static SearchResult<T> IndexOfSearch<T>(string text, string pattern, T value)
    {
        var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return new();

        var lengthDiff = Math.Abs(pattern.Length - text.Length);
        return new()
        {
            ExactMatch = true,
            Index = index,
            LengthDifference = lengthDiff,
            Match = text,
            Result = value,
        };
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
            .OrderBy(a => a);

    public static OrderedParallelQuery<SearchResult<T>> Search<T>(this ParallelQuery<T> enumerable, string query, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
        => SearchCollection(enumerable, query, selector, fuzzy, take, skip)
            .OrderBy(a => a);

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

                    var result = fuzzy ? DiceFuzzySearch(title, search, t) : IndexOfSearch(title, search, t);
                    if (result.CompareTo(current) >= 0)
                        return current;

                    return result;
                })
            )
            .Where(a => !string.IsNullOrEmpty(a?.Match));

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
        if (string.IsNullOrWhiteSpace(query) || user == null || limit <= 0)
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
                return anime != null && (tags.Count == 0 || !tags.FindInEnumerable(forbiddenTags));
            });
        var allTags = !flags.HasFlag(SearchFlags.Tags) ? null : RepoFactory.AniDB_Tag.GetAll()
            .AsParallel()
            .Where(tag => !forbiddenTags.Contains(tag.TagName) && !TagFilter.IsTagBlackListed(tag.TagName, tagFilter));
        return flags switch
        {
            SearchFlags.Titles => SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), false, limit)
                .OrderBy(a => a)
                .ToList(),
            SearchFlags.Fuzzy | SearchFlags.Titles => SearchCollection(allSeries, query, CreateSeriesTitleDelegate(), true, limit)
                .OrderBy(a => a)
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
            .OrderBy(a => a)
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
            .OrderBy(a => a)
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
                        ExactMatch = true,
                        Match = tag.TagName,
                        Result = series,
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
                        ExactMatch = true,
                        Distance = (600 - xref.Weight) / 600D,
                        Match = tag.TagName,
                        Result = series,
                    };
                })
                .Where(a => a != null)
            )
            .OrderBy(a => a)
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
                        ExactMatch = tag.ExactMatch,
                        Index = tag.Index,
                        Distance = tag.Distance,
                        LengthDifference = tag.LengthDifference,
                        Match = tag.Result.TagName,
                        Result = series,
                    };
                })
                .Where(b => b != null)
            )
            .OrderBy(a => a)
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
                        ExactMatch = tag.ExactMatch,
                        Index = tag.Index,
                        Distance = (tag.Distance + (600 - xref.Weight) / 600D) / 2,
                        LengthDifference = tag.LengthDifference,
                        Match = tag.Result.TagName,
                        Result = series,
                    };
                })
                .Where(a => a != null)
            )
            .OrderBy(a => a)
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
            .Select(title => title.Title)
            .Append(series.GetSeriesName())
            .Distinct();
    }

    public class SearchResult<T> : IComparable<SearchResult<T>>
    {
        /// <summary>
        /// Indicates whether the search result is an exact match to the query.
        /// </summary>
        public bool ExactMatch { get; set; } = false;

        /// <summary>
        /// Represents the position of the match within the sanitized string.
        /// This property is only applicable when ExactMatch is set to true.
        /// A lower value indicates a match that occurs earlier in the string.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// Represents the similarity measure between the sanitized query and the sanitized matched result.
        /// This may be the Levenshtein distance or the tag weight when comparing tags for a series.
        /// A lower value indicates a more similar match.
        /// </summary>
        public double Distance { get; set; } = 0D;

        /// <summary>
        /// Represents the absolute difference in length between the sanitized query and the sanitized matched result.
        /// A lower value indicates a match with a more similar length to the query.
        /// </summary>
        public int LengthDifference { get; set; } = 0;

        /// <summary>
        /// Contains the original matched substring from the original string.
        /// </summary>
        public string Match { get; set; } = string.Empty;

        /// <summary>
        /// Represents the result object associated with the match.
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Compares the current SearchResult instance with another SearchResult instance.
        /// The comparison is performed in the following order:
        /// 1. ExactMatch (descending): prioritize exact matches
        /// 2. Index (ascending): prioritize matches that occur earlier in the string
        /// 3. Distance (ascending): prioritize matches with smaller similarity distances
        /// 4. LengthDifference (ascending): prioritize matches with a more similar length to the query
        /// 5. Match (ascending): prioritize matches based on their lexicographic order
        /// </summary>
        /// <param name="other">The SearchResult instance to compare with the current instance.</param>
        /// <returns>A negative, zero, or positive integer indicating the relative order of the objects being compared.</returns>
        public int CompareTo(SearchResult<T> other)
        {
            if (other == null)
                return -1;

            var isEmpty = string.IsNullOrEmpty(Match);
            var otherIsEmpty = string.IsNullOrEmpty(other.Match);
            if (isEmpty && otherIsEmpty)
                return 0;
            if (isEmpty)
                return 1;
            if (otherIsEmpty)
                return -1;

            int exactMatchComparison = ExactMatch == other.ExactMatch ? 0 : ExactMatch ? -1 : 1;
            if (exactMatchComparison != 0)
                return exactMatchComparison;

            int indexComparison = Index.CompareTo(other.Index);
            if (indexComparison != 0)
                return indexComparison;

            int distanceComparison = Distance.CompareTo(other.Distance);
            if (distanceComparison != 0)
                return distanceComparison;

            int lengthDifferenceComparison = LengthDifference.CompareTo(other.LengthDifference);
            if (lengthDifferenceComparison != 0)
                return lengthDifferenceComparison;

            return string.Compare(Match, other.Match);
        }
    }
}
