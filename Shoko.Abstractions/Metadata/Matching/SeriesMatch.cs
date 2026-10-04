using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   One candidate a source offered, and how well it lines up with the anime.
/// </summary>
/// <remarks>
///   Not a link: a judgement about a search result, which the caller may act
///   on.
/// </remarks>
public sealed record SeriesMatch
{
    /// <summary>
    ///   The anime being matched.
    /// </summary>
    public required IAnidbAnime AnidbAnime { get; init; }

    /// <summary>
    ///   The candidate being judged, as the source handed it over.
    /// </summary>
    public required MetadataSeriesSearchResult Candidate { get; init; }

    /// <summary>
    ///   How the pairing was arrived at, and how much to trust it.
    /// </summary>
    public required MatchRating Rating { get; init; }

    /// <summary>
    ///   Why it was not taken, or <see cref="MatchRejectionReason.None"/> for
    ///   the one that was.
    /// </summary>
    public required MatchRejectionReason Rejection { get; init; }

    /// <summary>
    ///   What was compared, in words, for a candidate not taken: the anime's
    ///   date and episode count against the candidate's, how its episodes'
    ///   air dates lined up, and the one taken instead.
    ///   <c>null</c> for the one taken.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    ///   The candidate's season the anime lines up with best, or
    ///   <c>null</c> where the candidate carries no seasons. The
    ///   season of a conclusive <see cref="EpisodeAlignment"/> when there is
    ///   one.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    ///   Where the candidate's episodes line up with the anime's by their air
    ///   dates, or <c>null</c> when the source sent no dated
    ///   episodes, the anime has none or none aired on the same days.
    /// </summary>
    public EpisodeAlignment? EpisodeAlignment { get; init; }
}
