using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Services;

/// <summary>
///   Writes and reads the start season overrides as CSV: a header,
///   <c>AnidbAnimeID,Year,Season</c>, then one override per line, the
///   season by name (<c>Winter</c>, <c>Spring</c>, <c>Summer</c> or
///   <c>Fall</c>).
/// </summary>
public static class AnidbStartSeasonOverrideCsv
{
    #region Constants

    /// <summary>
    ///   The header line.
    /// </summary>
    public const string Header = "AnidbAnimeID,Year,Season";

    #endregion

    #region Export

    /// <summary>
    ///   Writes overrides as CSV, by ascending AniDB anime ID.
    /// </summary>
    /// <param name="overrides">The overrides.</param>
    /// <returns>The text, the header first, each line ending in <c>\n</c>.</returns>
    public static string Export(IEnumerable<AnidbStartSeasonOverride> overrides)
    {
        var output = new StringBuilder().Append(Header).Append('\n');
        foreach (var entry in overrides.OrderBy(entry => entry.AnidbAnimeID))
        {
            output
                .Append(entry.AnidbAnimeID.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(entry.Year.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(entry.Season.ToString())
                .Append('\n');
        }

        return output.ToString();
    }

    #endregion

    #region Import

    /// <summary>
    ///   Sets the overrides a CSV file lists through the AniDB service, and
    ///   reports what became of each line. Never removes an override the file
    ///   leaves out.
    /// </summary>
    /// <remarks>
    ///   Blank lines and lines starting with <c>#</c> are skipped, and so is
    ///   the header when it is the first line. Fields may be quoted. A line
    ///   is rejected when it does not hold three fields, an AniDB anime ID
    ///   above <c>0</c>, a year from 1900 to 9999 and a season by name, or
    ///   when an earlier line named the same anime.
    /// </remarks>
    /// <param name="reader">The CSV text.</param>
    /// <param name="anidbService">The AniDB service the overrides are set through.</param>
    /// <returns>The counts and the rejected lines.</returns>
    public static AnidbStartSeasonOverrideImportResult Import(TextReader reader, IAnidbService anidbService)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(anidbService);

        var (added, updated, unchanged) = (0, 0, 0);
        var rejected = new List<AnidbStartSeasonOverrideImportResult.RejectedLine>();
        var seen = new Dictionary<int, int>();
        var lineNumber = 0;
        var first = true;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var text = line.Trim();
            if (text.Length is 0 || text.StartsWith('#'))
                continue;

            var fields = text.Split(',').Select(field => Unquote(field.Trim())).ToArray();
            if (first)
            {
                first = false;
                if (IsHeader(fields))
                    continue;
            }

            if (Parse(fields, out var row) is { } reason)
            {
                rejected.Add(new(lineNumber, line, reason));
                continue;
            }

            if (seen.TryGetValue(row.AnimeID, out var earlier))
            {
                rejected.Add(new(lineNumber, line, $"AniDB anime {row.AnimeID} is already on line {earlier}."));
                continue;
            }

            seen[row.AnimeID] = lineNumber;
            var existing = anidbService.GetStartSeasonOverride(row.AnimeID);
            if (existing is not null && existing.Year == row.Year && existing.Season == row.Season)
            {
                unchanged++;
                continue;
            }

            anidbService.SetStartSeasonOverride(row.AnimeID, row.Year, row.Season);
            if (existing is null)
                added++;
            else
                updated++;
        }

        return new(added, updated, unchanged, rejected);
    }

    /// <summary>
    ///   Whether the fields of a line are the header's, ignoring case.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns><c>true</c> for the header.</returns>
    private static bool IsHeader(string[] fields)
        => string.Equals(string.Join(',', fields), Header, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   Reads an override from the fields of a line.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <param name="row">The override, when the fields hold one.</param>
    /// <returns>Why the line is rejected, or <c>null</c> when it holds an override.</returns>
    private static string? Parse(string[] fields, out (int AnimeID, int Year, YearlySeason Season) row)
    {
        row = default;
        if (fields.Length is not 3)
            return $"Expected 3 fields ({Header}), found {fields.Length}.";

        if (!int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var animeID) || animeID <= 0)
            return $"The AniDB anime ID \"{fields[0]}\" is not a number above 0.";

        if (!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            year is < AnidbStartSeasonOverrides.MinimumYear or > AnidbStartSeasonOverrides.MaximumYear)
            return $"The year \"{fields[1]}\" is not from {AnidbStartSeasonOverrides.MinimumYear} to {AnidbStartSeasonOverrides.MaximumYear}.";

        if (fields[2].Length is 0 || char.IsAsciiDigit(fields[2][0]) || fields[2][0] is '-' or '+' ||
            !Enum.TryParse<YearlySeason>(fields[2], ignoreCase: true, out var season) || !Enum.IsDefined(season))
            return $"The season \"{fields[2]}\" is not Winter, Spring, Summer or Fall.";

        row = (animeID, year, season);
        return null;
    }

    /// <summary>
    ///   A field without the double quotes around it, with doubled quotes
    ///   inside it made single.
    /// </summary>
    /// <param name="field">The trimmed field.</param>
    /// <returns>The value.</returns>
    private static string Unquote(string field)
        => field.Length >= 2 && field[0] is '"' && field[^1] is '"'
            ? field[1..^1].Replace("\"\"", "\"").Trim()
            : field;

    #endregion
}

/// <summary>
///   What a CSV import of start season overrides did.
/// </summary>
/// <param name="Added">How many overrides were set for anime without one.</param>
/// <param name="Updated">How many existing overrides were changed.</param>
/// <param name="Unchanged">How many lines matched the override already set.</param>
/// <param name="Rejected">The lines that could not be imported, in file order.</param>
public sealed record AnidbStartSeasonOverrideImportResult(
    int Added,
    int Updated,
    int Unchanged,
    IReadOnlyList<AnidbStartSeasonOverrideImportResult.RejectedLine> Rejected
)
{
    /// <summary>
    ///   A line that could not be imported.
    /// </summary>
    /// <param name="Line">The line's number, from <c>1</c>.</param>
    /// <param name="Text">The line as it was in the file.</param>
    /// <param name="Reason">Why it was rejected.</param>
    public sealed record RejectedLine(int Line, string Text, string Reason);
}
