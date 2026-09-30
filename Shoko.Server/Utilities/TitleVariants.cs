using System.Text.RegularExpressions;

namespace Shoko.Server.Utilities;

/// <summary>
///   The shorter forms of a title a search tries and matching judges by: the
///   title without a sequel suffix, and without its subtitle.
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
    ///   was all there was; or <see langword="null"/> when it has none.
    /// </returns>
    public static string? WithoutSequelSuffix(string title)
        => SequelSuffixRegex().Match(title) is { Success: true } suffix ? title[..^suffix.Length].TrimEnd() : null;

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
    ///   The title up to the subtitle, or <see langword="null"/> when it has
    ///   none.
    /// </returns>
    public static string? WithoutSubtitle(string title, bool isJapanese)
    {
        var index = title.IndexOf(isJapanese ? ' ' : ':');
        return index > 0 ? title[..index] : null;
    }

    #endregion
}
