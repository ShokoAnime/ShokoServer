using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

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

    private static string SanitizeSearchInput(this string value) =>
        value.Normalize(NormalizationForm.FormKC)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .CompactWhitespaces()
            .ToLowerInvariant();

    // This forces ASCII, because it's faster to stop caring if ss and ß are the same
    // No it's not perfect, but it works better for those who just want to do lazy searching
    private static string ForceASCII(this string value) =>
        value.Normalize(NormalizationForm.FormKC)
            .FilterSearchCharacters()
            .CompactWhitespaces()
            .ToLowerInvariant();

    private static readonly IStringDistance DiceSearch = new SorensenDice();

    private static volatile FuzzySearchIndex<AnimeSeries> _seriesIndex;
    private static volatile bool _isDirty = true;
    private static readonly object _indexLock = new();

    public static void MarkDirty() => _isDirty = true;

    private static FuzzySearchIndex<AnimeSeries> EnsureSeriesIndex()
    {
        if (!_isDirty && _seriesIndex != null)
            return _seriesIndex;
        lock (_indexLock)
        {
            if (!_isDirty && _seriesIndex != null)
                return _seriesIndex;
            var idx = new FuzzySearchIndex<AnimeSeries>();
            idx.Build(RepoFactory.AnimeSeries.GetAll(), CreateSeriesTitleDelegate());
            _seriesIndex = idx;
            _isDirty = false;
            return idx;
        }
    }

    internal static bool IsLatinScript(string input)
    {
        foreach (var c in input)
            if (char.IsLetter(c) && c > 'ɏ')
                return false;
        return true;
    }

    internal static string NormalizeForIndex(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var decomposed = input.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            // U+301C 〜 survives NFKD unchanged; '~'/'|' join it as decorative separators (e.g. "~X~"/": X", "A | B"/"A / B").
            sb.Append(c is '-' or '_' or '.' or ':' or ',' or '!' or ';' or '/' or '\\' or '(' or ')' or '[' or ']' or '〜' or '~' or '|' ? ' ' : c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormKC).ToLowerInvariant().CompactWhitespaces();
    }

    internal static int GetMaxErrors(int queryLength)
        => Math.Min(queryLength / 4, 3);

    /// <summary>
    /// Wagner-Fischer distance comparison
    /// </summary>
    /// <param name="query"></param>
    /// <param name="text"></param>
    /// <returns></returns>
    internal static int MinEditDistInText(string query, string text)
    {
        var m = query.Length;
        var n = text.Length;
        if (m == 0) return 0;
        if (n == 0) return m;

        // prevprev, prev, curr are the rolling DP rows (i-2, i-1, i).
        // Row 0 stays all-zeros throughout (free text-prefix skip for semi-global matching).
        var prevprev = new int[n + 1]; // row i-2; starts as the virtual row 0 (all zeros)
        var prev = new int[n + 1];     // row i-1; starts as the virtual row 0 (all zeros)
        var curr = new int[n + 1];

        for (var i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= n; j++)
            {
                var cost = query[i - 1] == text[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
                // OSA: adjacent transposition costs 1 instead of 2.
                if (i > 1 && j > 1 && query[i - 1] == text[j - 2] && query[i - 2] == text[j - 1])
                    curr[j] = Math.Min(curr[j], prevprev[j - 2] + 1);
            }
            (prevprev, prev, curr) = (prev, curr, prevprev);
        }

        var min = prev[0];
        for (var j = 1; j <= n; j++)
            if (prev[j] < min) min = prev[j];
        return min;
    }

    public static SearchResult<T> DiceFuzzySearch<T>(string text, string pattern, T value)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(pattern))
            return new();

        // Sanitize inputs before use.
        text = text.SanitizeSearchInput();
        pattern = pattern.SanitizeSearchInput();
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(text))
            return new();

        // Strict search for any text (e.g. ASCII, Japanese Kanji/Kana, etc.).
        var index = text.IndexOf(pattern, StringComparison.Ordinal);
        if (index > -1)
            return new()
            {
                ExactMatch = true,
                Index = index,
                LengthDifference = Math.Abs(pattern.Length - text.Length),
                Match = text,
                Result = value,
            };

        // If strict search didn't work, then force ASCII and do a fuzzy search.
        text = text.ForceASCII();
        pattern = pattern.ForceASCII();
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(text))
            return new();

        // Always search the longer string for the shorter one.
        var match = text;
        if (pattern.Length > text.Length)
            (text, pattern) = (pattern, text);

        var result = DiceSearch.Distance(text, pattern);

        // Don't count an error as liberally when the title is short.
        if (text.Length < 5 && result > 0.5)
            return new();

        if (result >= 0.8)
            return new();

        return new()
        {
            Distance = result,
            LengthDifference = Math.Abs(pattern.Length - text.Length),
            Match = match,
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
        if (string.IsNullOrWhiteSpace(result.Match))
            return false;
        if (result.ExactMatch)
            return true;
        if (text.Length <= 5 && result.Distance > 0.5D)
            return false;
        return result.Distance < 0.8D;
    }

    public static IEnumerable<SearchResult<T>> Search<T>(this IEnumerable<T> enumerable, string query, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
        => SearchCollection(enumerable is ParallelQuery<T> parallel ? parallel : enumerable.AsParallel(), query, selector, fuzzy, take, skip);

    public static ParallelQuery<SearchResult<T>> Search<T>(this ParallelQuery<T> enumerable, string query, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
        => SearchCollection(enumerable, query, selector, fuzzy, take, skip);

    private static ParallelQuery<SearchResult<T>> SearchCollection<T>(ParallelQuery<T> items, string search, Func<T, IEnumerable<string>> selector, bool fuzzy = false, int? take = null, int? skip = null)
    {
        if (take.HasValue && take.Value <= 0)
            return new List<SearchResult<T>>().AsParallel();

        var normalizedSearch = NormalizeForIndex(search);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
            return new List<SearchResult<T>>().AsParallel();

        var isLatin = IsLatinScript(normalizedSearch);
        var maxErrors = fuzzy && isLatin ? GetMaxErrors(normalizedSearch.Length) : 0;

        ParallelQuery<SearchResult<T>> enumerable = items
            .Select(t =>
            {
                SearchResult<T> best = null;
                foreach (var title in selector(t))
                {
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var normalizedTitle = NormalizeForIndex(title);
                    if (string.IsNullOrWhiteSpace(normalizedTitle))
                        continue;

                    SearchResult<T> result;
                    if (!isLatin)
                    {
                        var idx = normalizedTitle.IndexOf(normalizedSearch, StringComparison.Ordinal);
                        if (idx < 0)
                            continue;
                        result = new SearchResult<T>
                        {
                            ExactMatch = true,
                            Index = idx,
                            Distance = 0,
                            LengthDifference = Math.Abs(normalizedSearch.Length - normalizedTitle.Length),
                            Match = title,
                            Result = t,
                        };
                    }
                    else
                    {
                        var containsIdx = normalizedTitle.IndexOf(normalizedSearch, StringComparison.Ordinal);
                        if (containsIdx >= 0)
                        {
                            result = new SearchResult<T>
                            {
                                ExactMatch = true,
                                Index = containsIdx,
                                Distance = 0,
                                LengthDifference = Math.Abs(normalizedSearch.Length - normalizedTitle.Length),
                                Match = title,
                                Result = t,
                            };
                        }
                        else if (fuzzy && IsLatinScript(normalizedTitle) && normalizedTitle.Length >= normalizedSearch.Length - maxErrors)
                        {
                            var dist = MinEditDistInText(normalizedSearch, normalizedTitle);
                            if (dist > maxErrors)
                                continue;
                            result = new SearchResult<T>
                            {
                                ExactMatch = false,
                                Index = 0,
                                Distance = normalizedSearch.Length > 0 ? (double)dist / normalizedSearch.Length : 0,
                                LengthDifference = Math.Abs(normalizedSearch.Length - normalizedTitle.Length),
                                Match = title,
                                Result = t,
                            };
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (result.CompareTo(best) < 0)
                        best = result;
                }

                return best;
            })
            .Where(a => a != null && !string.IsNullOrEmpty(a.Match))
            .OrderBy(a => a);

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
    /// <param name="searchById">Enable search by anidb anime id.</param>
    /// <returns>
    ///     <see cref="List{SearchResult}" />
    /// </returns>
    public static List<SearchResult<AnimeSeries>> SearchSeries(JMMUser user, string query, int limit, SearchFlags flags,
            TagFilter.Filter tagFilter = TagFilter.Filter.None, bool searchById = false)
    {
        if (string.IsNullOrWhiteSpace(query) || user == null || limit <= 0)
            return [];

        query = query.ToLowerInvariant();
        var forbiddenTags = user.GetHideCategories();

        if (searchById && int.TryParse(query, out var animeID))
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            var anime = series?.AniDB_Anime;
            var tags = anime?.GetAllTags();
            if (anime != null && !tags.FindInEnumerable(forbiddenTags))
                return
                [
                    new()
                    {
                        ExactMatch = true,
                        Match = series.AniDB_ID.ToString(),
                        Result = series,
                    },
                ];
        }

        var allTags = !flags.HasFlag(SearchFlags.Tags) ? null : RepoFactory.AniDB_Tag.GetAll()
            .AsParallel()
            .Where(tag => !forbiddenTags.Contains(tag.TagName) && !TagFilter.IsTagBlackListed(tag.TagName, tagFilter));
        return flags switch
        {
            SearchFlags.Titles => SearchTitles(query, limit, forbiddenTags, fuzzy: false),
            SearchFlags.Fuzzy | SearchFlags.Titles => SearchTitles(query, limit, forbiddenTags, fuzzy: true),
            SearchFlags.Tags => SearchTagsExact(query, limit, forbiddenTags, allTags),
            SearchFlags.Fuzzy | SearchFlags.Tags => SearchTagsFuzzy(query, limit, forbiddenTags, allTags),
            SearchFlags.Tags | SearchFlags.Titles => SearchTitleAndTagsCombined(query, limit, forbiddenTags, allTags, fuzzy: false),
            SearchFlags.Fuzzy | SearchFlags.Tags | SearchFlags.Titles => SearchTitleAndTagsCombined(query, limit, forbiddenTags, allTags, fuzzy: true),
            _ => [],
        };
    }

    private static List<SearchResult<AnimeSeries>> SearchTitles(string query, int limit, HashSet<string> forbiddenTags, bool fuzzy)
        => EnsureSeriesIndex()
            .Search(query, fuzzy)
            .Where(r =>
            {
                var anime = r.Result.AniDB_Anime;
                var tags = anime?.GetAllTags();
                return anime != null && (tags.Count == 0 || !tags.FindInEnumerable(forbiddenTags));
            })
            .Take(limit)
            .ToList();

    private static List<SearchResult<AnimeSeries>> SearchTitleAndTagsCombined(
        string query, int limit, HashSet<string> forbiddenTags, ParallelQuery<AniDB_Tag> allTags, bool fuzzy)
    {
        var titleResults = SearchTitles(query, limit, forbiddenTags, fuzzy);
        var tagLimit = limit - titleResults.Count;
        if (tagLimit > 0)
            titleResults.AddRange(fuzzy
                ? SearchTagsFuzzy(query, tagLimit, forbiddenTags, allTags)
                : SearchTagsExact(query, tagLimit, forbiddenTags, allTags));
        return titleResults;
    }

    private static List<SearchResult<AnimeSeries>> SearchTagsExact(string query, int limit,
        HashSet<string> forbiddenTags, ParallelQuery<AniDB_Tag> allTags)
    {
        var seriesList = new List<SearchResult<AnimeSeries>>();
        seriesList.AddRange(RepoFactory.CustomTag.GetAll()
            .Where(tag => tag.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                    var anime = series?.AniDB_Anime;
                    var tags = anime?.GetAllTags();
                    if (anime == null || tags.Count == 0 || tags.FindInEnumerable(forbiddenTags))
                        return null;

                    return new SearchResult<AnimeSeries>
                    {
                        ExactMatch = true,
                        Match = tag.TagName,
                        Result = series,
                    };
                })
                .WhereNotNull()
            )
            .OrderBy(a => a.Result.PreferredTitle)
            .Take(limit)
        );

        limit -= seriesList.Count;

        seriesList.AddRange(allTags
            .Where(a => a.TagName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(tag => RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    var anime = series?.AniDB_Anime;
                    var tags = anime?.GetAllTags();
                    if (anime == null || tags.Count == 0 || tags.FindInEnumerable(forbiddenTags))
                        return null;

                    return new SearchResult<AnimeSeries>
                    {
                        ExactMatch = true,
                        Distance = (600 - xref.Weight) / 600D,
                        Match = tag.TagName,
                        Result = series,
                    };
                })
                .WhereNotNull()
            )
            .OrderBy(a => a)
            .ThenBy(a => a.Result.PreferredTitle)
            .Take(limit)
        );
        return seriesList;
    }

    private static List<SearchResult<AnimeSeries>> SearchTagsFuzzy(string query, int limit,
        HashSet<string> forbiddenTags, ParallelQuery<AniDB_Tag> allTags)
    {
        var seriesList = new List<SearchResult<AnimeSeries>>();
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
            .WhereNotNull();
        seriesList.AddRange(customTags
            .SelectMany(tag => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.Result.CustomTagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID);
                    var anime = series?.AniDB_Anime;
                    var tags = anime?.GetAllTags();
                    if (anime == null || tags.Count == 0 || tags.FindInEnumerable(forbiddenTags))
                        return null;

                    return new SearchResult<AnimeSeries>
                    {
                        ExactMatch = tag.ExactMatch,
                        Index = tag.Index,
                        Distance = tag.Distance,
                        LengthDifference = tag.LengthDifference,
                        Match = tag.Result.TagName,
                        Result = series,
                    };
                })
                .WhereNotNull()
            )
            .OrderBy(a => a)
            .ThenBy(b => b.Result.PreferredTitle)
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
            .WhereNotNull();
        seriesList.AddRange(tags
            .SelectMany(tag => RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.Result.TagID)
                .Select(xref =>
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    var anime = series?.AniDB_Anime;
                    var tags = anime?.GetAllTags();
                    if (anime == null || tags.Count == 0 || tags.FindInEnumerable(forbiddenTags))
                        return null;

                    return new SearchResult<AnimeSeries>
                    {
                        ExactMatch = tag.ExactMatch,
                        Index = tag.Index,
                        Distance = (tag.Distance + (600 - xref.Weight) / 600D) / 2,
                        LengthDifference = tag.LengthDifference,
                        Match = tag.Result.TagName,
                        Result = series,
                    };
                })
                .WhereNotNull()
            )
            .OrderBy(a => a)
            .ThenBy(a => a.Result.PreferredTitle)
            .Take(limit)
        );
        return seriesList;
    }

    private static Func<AnimeSeries, IEnumerable<string>> CreateSeriesTitleDelegate()
    {
        var settings = ISettingsProvider.Instance.GetSettings();
        var languages = new HashSet<string> { "en", "x-jat" };
        languages.UnionWith(settings.Language.SeriesTitleLanguageOrder);
        return series => RepoFactory.AniDB_Anime_Title.GetByAnimeID(series.AniDB_ID)
            .Where(title => title.TitleType is TitleType.Main || languages.Contains(title.LanguageCode))
            .Select(title => title.Title)
            .Append(series.Title)
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
        /// This may be the sorensen-dice distance or the tag weight when comparing tags for a series.
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

            var exactMatchComparison = ExactMatch == other.ExactMatch ? 0 : ExactMatch ? -1 : 1;
            if (exactMatchComparison != 0)
                return exactMatchComparison;

            var indexComparison = Index.CompareTo(other.Index);
            if (indexComparison != 0)
                return indexComparison;

            var distanceComparison = Distance.CompareTo(other.Distance);
            if (distanceComparison != 0)
                return distanceComparison;

            var lengthDifferenceComparison = LengthDifference.CompareTo(other.LengthDifference);
            if (lengthDifferenceComparison != 0)
                return lengthDifferenceComparison;

            return string.Compare(Match, other.Match);
        }

        public SearchResult<Y> Map<Y>(Y result)
            => new()
            {
                ExactMatch = ExactMatch,
                Index = Index,
                Distance = Distance,
                LengthDifference = LengthDifference,
                Match = Match,
                Result = result,
            };
    }
}
