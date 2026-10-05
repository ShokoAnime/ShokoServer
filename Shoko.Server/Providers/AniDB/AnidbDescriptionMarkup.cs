using System.Text.RegularExpressions;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   Reads AniDB's description markup: its italic and bold tags, its links,
///   and the notes it puts around or in place of a synopsis.
/// </summary>
public static partial class AnidbDescriptionMarkup
{
    #region Plain Text

    /// <summary>
    ///   Drops AniDB's italic and bold markup and keeps only the text of its
    ///   links.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The plain text, empty for <c>null</c>.</returns>
    public static string ToPlainText(string? description)
        => AnidbLink().Replace(Markup().Replace(description ?? string.Empty, string.Empty), "$1");

    #endregion

    #region Notes

    /// <summary>
    ///   Whether a description holds only notes and no synopsis, such as
    ///   <c>* Based on a manga by …</c> on its own.
    /// </summary>
    /// <remarks>
    ///   Every line with text must be a note: one starting with <c>*</c>,
    ///   <c>Note:</c> or <c>Note 2:</c>, or a <c>Source:</c> or
    ///   <c>Summary by …</c> credit. A single line of prose is a synopsis.
    /// </remarks>
    /// <param name="description">The description, with AniDB's markup.</param>
    /// <returns><c>true</c> when the description has text and all of it is notes.</returns>
    public static bool IsNoteOnly(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var hasText = false;
        foreach (var rawLine in ToPlainText(description).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length is 0)
                continue;

            if (!NoteLine().IsMatch(line))
                return false;
            hasText = true;
        }

        return hasText;
    }

    #endregion

    #region Patterns

    [GeneratedRegex(@"\[/?[ib]\]", RegexOptions.CultureInvariant)]
    private static partial Regex Markup();

    [GeneratedRegex(@"https?://anidb\.net/\S+ \[([^\]]*)\]", RegexOptions.CultureInvariant)]
    private static partial Regex AnidbLink();

    /// <summary>
    ///   A line that is a note: a bullet, a numbered or plain note, or a
    ///   credit for where the text came from. The colon is required on each
    ///   label, so prose that only mentions a source never matches.
    /// </summary>
    [GeneratedRegex(
        @"^(?:\*|Note(?:\s+\d+)?\s*:|Sources?\s*:|Summary(?:\s*:|\s+(?:written\s+)?by\b))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex NoteLine();

    #endregion
}
