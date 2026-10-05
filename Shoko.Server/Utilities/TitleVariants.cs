using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Server.Services;

namespace Shoko.Server.Utilities;

/// <summary>
///   The shorter forms of a title a search tries and matching judges by: the
///   title without a sequel suffix, and without its subtitle; and the sequel
///   number it carries.
/// </summary>
/// <remarks>
///   Kept in one place so the query a source is searched with and the query
///   its candidates are judged by are cut the same way.
/// </remarks>
internal static partial class TitleVariants
{
    #region Sequel suffix

    /// <summary>
    ///   A sequel suffix at the end of a title, such as a year in brackets,
    ///   "Season 2", "S2", "2nd Season", "第2期" or "第二期". The number
    ///   after 第 may be written in any digits, full-width ones included, or
    ///   in kanji numerals.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"\(\d{4}\)$|\bs(?:eason)? (?:\d+|(?=[MDCLXVI])M*(?:C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3}))$|\bs\d+$|第[\d零〇一二三四五六七八九十百千萬億兆京垓點]+[季期]$|\b(?:second|2nd|third|3rd|fourth|4th|fifth|5th|sixth|6th) season$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SequelSuffixRegex();

    /// <summary>
    ///   The title without its sequel suffix.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>
    ///   The title up to the suffix, trimmed, which is empty when the suffix
    ///   was all there was; or <c>null</c> when it has none.
    /// </returns>
    public static string? WithoutSequelSuffix(string title)
        => SequelSuffixRegex().Match(title) is { Success: true } suffix ? title[..^suffix.Length].TrimEnd() : null;

    #endregion

    #region Sequel number

    /// <summary>
    ///   The ordinal words a title may number its season with.
    /// </summary>
    private static readonly string[] _ordinalWords = ["first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];

    /// <summary>
    ///   A sequel or season number at the end of a title: "Season 2",
    ///   "Season II", "S2", "2nd Season", "Second Season", "Part 2",
    ///   "Cour 2", "第2期", "第二期", "2期", a trailing roman numeral from II,
    ///   or a trailing number of one or two digits.
    /// </summary>
    /// <remarks>
    ///   The number is in the group <c>n</c> (digits or kanji numerals),
    ///   <c>r</c> (roman) or <c>w</c> (a word), and <c>part</c> is set for a
    ///   part or a cour.
    /// </remarks>
    /// <returns>The expression.</returns>
    [GeneratedRegex(
        @"(?:\bseason\s*(?<n>\d+)|\bseason\s+(?<r>[IVX]+)|\bs(?<n>\d+)|\b(?<n>\d+)(?:st|nd|rd|th)\s+season" +
        @"|\b(?<w>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth)\s+season" +
        @"|\b(?<part>part|cour)\s*(?<n>\d+)|\b(?<part>part|cour)\s+(?<r>[IVX]+)|第(?<n>[\d〇零一二三四五六七八九十百千两兩]+)[期季]|(?<n>\d+)期" +
        @"|\s(?-i:(?<r>II|III|IV|VI|VII|VIII|IX))|(?:\s|(?<=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]))(?<n>\d{1,2}))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex SequelNumberRegex();

    /// <summary>
    ///   A year in brackets at the end of a title, which tells a work apart
    ///   from another of the same name but says nothing of its number.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"\(\d{4}\)$", RegexOptions.CultureInvariant)]
    private static partial Regex YearSuffixRegex();

    /// <summary>
    ///   The sequel or season number a title carries at its end, or before its
    ///   subtitle. A part or cour counts only without a season number.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>
    ///   The number; <c>1</c> when the title carries none; <c>null</c> when it
    ///   is empty or carries only a year.
    /// </returns>
    public static int? SequelNumber(string? title)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title) || YearSuffixRegex().IsMatch(title))
            return null;

        if (NumberAtEnd(title) is { } number)
            return number;

        var colon = title.IndexOf(':');
        return colon > 0 && NumberAtEnd(title[..colon].TrimEnd()) is { } beforeSubtitle ? beforeSubtitle : 1;
    }

    /// <summary>
    ///   The number at the end of a title, a season's before a part's.
    /// </summary>
    /// <param name="title">The title, trimmed.</param>
    /// <returns>The number, or <c>null</c> when it ends in none.</returns>
    private static int? NumberAtEnd(string title)
    {
        if (SequelNumberRegex().Match(title) is not { Success: true } match)
            return null;

        var number = NumberOf(match);
        if (!match.Groups["part"].Success)
            return number;

        // "Season 3 Part 2" is the third season's second half.
        var rest = title[..match.Index].TrimEnd();
        return SequelNumberRegex().Match(rest) is { Success: true } season && !season.Groups["part"].Success ? NumberOf(season) ?? number : number;
    }

    /// <summary>
    ///   The number one match of <see cref="SequelNumberRegex"/> carries.
    /// </summary>
    /// <param name="match">The match.</param>
    /// <returns>The number, or <c>null</c> when it cannot be read.</returns>
    private static int? NumberOf(Match match)
    {
        if (match.Groups["w"].Success)
            return Array.IndexOf(_ordinalWords, match.Groups["w"].Value.ToLowerInvariant()) + 1;

        if (match.Groups["r"].Success)
            return RomanValue(match.Groups["r"].Value);

        return GenericEpisodeTitles.ParseNumber(match.Groups["n"].Value);
    }

    /// <summary>
    ///   The value of a roman numeral made of I, V and X.
    /// </summary>
    /// <param name="numeral">The numeral, in any case.</param>
    /// <returns>The value, or <c>null</c> for an empty one.</returns>
    private static int? RomanValue(string numeral)
    {
        var total = 0;
        var previous = 0;
        foreach (var character in numeral.ToUpperInvariant().Reverse())
        {
            var value = character switch { 'I' => 1, 'V' => 5, 'X' => 10, _ => 0 };
            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total > 0 ? total : null;
    }

    #endregion

    #region Subtitle

    /// <summary>
    ///   The title without its subtitle: up to the first colon, or the first
    ///   space for a Japanese title, which marks it that way instead.
    /// </summary>
    /// <param name="title">
    ///   The title, already without its sequel suffix where it had one.
    /// </param>
    /// <param name="isJapanese">Whether the title is written in Japanese.</param>
    /// <returns>
    ///   The title up to the subtitle, or <c>null</c> when it has
    ///   none.
    /// </returns>
    public static string? WithoutSubtitle(string title, bool isJapanese)
    {
        var index = title.IndexOf(isJapanese ? ' ' : ':');
        return index > 0 ? title[..index] : null;
    }

    #endregion
}
