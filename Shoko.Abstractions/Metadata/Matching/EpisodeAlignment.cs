namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   Where a candidate's episodes line up with the anime's by their air
///   dates: one of its seasons, and how far its numbering runs ahead.
/// </summary>
/// <remarks>
///   Worked out from the episodes a source sent with a season
///   (<see cref="Search.MetadataSearchResultSeason.Episodes"/>) against the
///   anime's dated regular episodes. Each pair aired within a day of each
///   other votes for a season and an offset; the one with the most days wins.
/// </remarks>
public sealed record EpisodeAlignment
{
    /// <summary>
    ///   The candidate's season the anime lines up with.
    /// </summary>
    public required int SeasonNumber { get; init; }

    /// <summary>
    ///   The candidate's episode number less the AniDB episode number it
    ///   lines up with, such as 12 for an anime that is the second half of a
    ///   24-episode season.
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    ///   How many of <see cref="DatedEpisodes"/> aired within a day of the
    ///   season's episode they line up with.
    /// </summary>
    public required int MatchedEpisodes { get; init; }

    /// <summary>
    ///   How many of the anime's dated regular episodes line up, at that
    ///   offset, with a dated episode of the season. An episode undated on
    ///   either side is left out, telling nothing.
    /// </summary>
    public required int DatedEpisodes { get; init; }

    /// <summary>
    ///   How many different days the matched episodes aired on, so a double
    ///   episode or a double premiere counts once.
    /// </summary>
    public required int MatchedDays { get; init; }

    /// <summary>
    ///   The share of <see cref="DatedEpisodes"/> that matched, from zero to
    ///   one.
    /// </summary>
    public double Coverage => DatedEpisodes is 0 ? 0 : (double)MatchedEpisodes / DatedEpisodes;

    /// <summary>
    ///   Whether it is strong enough to count as the dates agreeing: enough
    ///   days matched and nearly all of the stretch. A weak one never changes
    ///   a rating.
    /// </summary>
    public required bool IsConclusive { get; init; }
}
