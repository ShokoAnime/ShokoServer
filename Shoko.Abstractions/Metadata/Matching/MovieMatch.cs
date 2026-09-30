using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   One film a source offered, and how well it lines up with the episode
///   standing for it.
/// </summary>
/// <remarks>
///   Not a link: a judgement about a search result, which the caller may act
///   on.
/// </remarks>
public sealed record MovieMatch
{
    /// <summary>
    ///   The anime the film belongs to, whose title names it.
    /// </summary>
    public required IAnidbAnime AnidbAnime { get; init; }

    /// <summary>
    ///   The episode standing for the film, which a film is claimed against.
    /// </summary>
    public required IAnidbEpisode AnidbEpisode { get; init; }

    /// <summary>
    ///   The candidate being judged, as the source handed it over.
    /// </summary>
    public required MetadataMovieSearchResult Candidate { get; init; }

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
    ///   What was compared, in words, for a candidate not taken: the year
    ///   looked for against the film's, and the one taken instead.
    ///   <see langword="null"/> for the one taken.
    /// </summary>
    public string? Details { get; init; }
}
