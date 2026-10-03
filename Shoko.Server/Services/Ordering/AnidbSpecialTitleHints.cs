using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.Server.Services.Ordering;

/// <summary>
///   Reads where an AniDB special airs from its titles, such as
///   <c>Episode 17.5</c> or <c>第17.5話</c> for a special after episode 17.
/// </summary>
/// <remarks>
///   Only an episode word or <c>#</c> before an <c>N.M</c> number counts, so
///   <c>Stage 17.5</c>, <c>Ver.0.5</c>, dates and <c>Evangelion: 3.0</c> never
///   do. <c>Episode 0</c> stands before episode 1.
/// </remarks>
internal static partial class AnidbSpecialTitleHints
{
    #region Patterns

    /// <summary>
    ///   An English or romaji <c>Episode N.M</c>, <c>Ep. N.M</c> or <c>#N.M</c>.
    /// </summary>
    [GeneratedRegex(@"(?<!\p{L})(?:episode|epsiode|ep\.?|#)\s?(?<n>\d+)\.(?<m>\d+)(?!\d|\.\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatinRegex();

    /// <summary>
    ///   An English or romaji <c>Episode 0</c>, <c>Episode 00</c> or <c>ep0</c>.
    /// </summary>
    [GeneratedRegex(@"(?<!\p{L})(?:episode|epsiode|ep\.?)\s?0+(?!\d|\.\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatinZeroRegex();

    /// <summary>
    ///   A Japanese <c>第N.M話</c> or <c>第N.M回</c>, in half or full width.
    /// </summary>
    [GeneratedRegex(@"第\s?(?<n>[0-9０-９]+)[.．](?<m>[0-9０-９]+)\s?[話回]", RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseRegex();

    /// <summary>
    ///   A Japanese <c>第0話</c> or <c>第0回</c>, in half or full width.
    /// </summary>
    [GeneratedRegex(@"第\s?[0０]+\s?[話回]", RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseZeroRegex();

    #endregion

    #region Parsing

    /// <summary>
    ///   Reads where a special airs from its titles. The English title is
    ///   tried first, then the romaji and the Japanese ones.
    /// </summary>
    /// <param name="englishTitle">The special's English title, if any.</param>
    /// <param name="romajiTitle">The special's romaji (x-jat) title, if any.</param>
    /// <param name="japaneseTitle">The special's Japanese title, if any.</param>
    /// <param name="regularEpisodeCount">How many regular episodes the anime has.</param>
    /// <returns>The hint, or <c>null</c> when no title gives one in range.</returns>
    internal static AnidbSpecialTitleHint? Parse(string? englishTitle, string? romajiTitle, string? japaneseTitle, int regularEpisodeCount)
        => ParseLatin(englishTitle, regularEpisodeCount) ??
            ParseLatin(romajiTitle, regularEpisodeCount) ??
            ParseJapanese(japaneseTitle, regularEpisodeCount);

    /// <summary>
    ///   Puts specials in the order they air by their hints: by the episode
    ///   they follow, then the sort key, then the AniDB special number.
    /// </summary>
    /// <param name="specials">The specials, each with a hint.</param>
    /// <param name="hint">Gets a special's hint.</param>
    /// <param name="specialNumber">Gets a special's AniDB special number.</param>
    /// <typeparam name="T">The type of the specials.</typeparam>
    /// <returns>The specials, in airing order.</returns>
    internal static IEnumerable<T> InAiringOrder<T>(IEnumerable<T> specials, Func<T, AnidbSpecialTitleHint> hint, Func<T, int> specialNumber)
        => specials
            .OrderBy(special => hint(special).AfterEpisode)
            .ThenBy(special => hint(special).SortKey)
            .ThenBy(specialNumber);

    /// <summary>
    ///   Reads a hint from an English or romaji title.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="regularEpisodeCount">How many regular episodes the anime has.</param>
    /// <returns>The hint, or <c>null</c>.</returns>
    private static AnidbSpecialTitleHint? ParseLatin(string? title, int regularEpisodeCount)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (LatinRegex().Match(title) is { Success: true } match)
            return Hint(match, regularEpisodeCount);

        return LatinZeroRegex().IsMatch(title) ? new(0, 0) : null;
    }

    /// <summary>
    ///   Reads a hint from a Japanese title.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="regularEpisodeCount">How many regular episodes the anime has.</param>
    /// <returns>The hint, or <c>null</c>.</returns>
    private static AnidbSpecialTitleHint? ParseJapanese(string? title, int regularEpisodeCount)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (JapaneseRegex().Match(title) is { Success: true } match)
            return Hint(match, regularEpisodeCount);

        return JapaneseZeroRegex().IsMatch(title) ? new(0, 0) : null;
    }

    /// <summary>
    ///   Makes a hint from a matched <c>N.M</c>, when N is in range.
    /// </summary>
    /// <param name="match">The match, with <c>n</c> and <c>m</c> groups.</param>
    /// <param name="regularEpisodeCount">How many regular episodes the anime has.</param>
    /// <returns>The hint, or <c>null</c> when N is out of range.</returns>
    private static AnidbSpecialTitleHint? Hint(Match match, int regularEpisodeCount)
    {
        if (Number(match.Groups["n"].Value) is not { } afterEpisode || afterEpisode > regularEpisodeCount)
            return null;

        return Number(match.Groups["m"].Value) is { } sortKey ? new(afterEpisode, sortKey) : null;
    }

    /// <summary>
    ///   Reads a number of half or full width digits.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The number, or <c>null</c> when it is too large.</returns>
    private static int? Number(string digits)
    {
        var value = 0L;
        foreach (var digit in digits)
        {
            value = value * 10 + (long)char.GetNumericValue(digit);
            if (value > int.MaxValue)
                return null;
        }

        return (int)value;
    }

    #endregion
}

/// <summary>
///   Where an AniDB special airs, as its title says.
/// </summary>
/// <param name="AfterEpisode">The regular episode it airs after; 0 for before episode 1.</param>
/// <param name="SortKey">The number after the dot, ordering specials after the same episode.</param>
internal readonly record struct AnidbSpecialTitleHint(int AfterEpisode, int SortKey);
