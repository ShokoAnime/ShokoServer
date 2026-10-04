using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   One series or film a source's auto-linking scored for an anime, taken
///   or turned down.
/// </summary>
/// <remarks>
///   The source hands these back and the core links the ones taken, so a
///   preview shows what an automatic search would do, and why the rest lost.
/// </remarks>
public sealed record MetadataAutoLinkCandidate
{
    /// <summary>
    ///   The series or film, as a search would have offered it.
    /// </summary>
    public required MetadataSearchResult Result { get; init; }

    /// <summary>
    ///   The candidate's identity at the source.
    /// </summary>
    public MetadataGuid ID => Result.ID;

    /// <summary>
    ///   The AniDB anime the candidate would be linked to.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The AniDB episode a film would stand for, or <c>null</c>
    ///   for a link to the whole anime.
    /// </summary>
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    ///   How the match was arrived at, which the link is written with. A link
    ///   of the anime's own keeps its rating here; one of a prequel's is
    ///   <see cref="MatchRating.None"/>, its rating being the prequel's and
    ///   not this anime's.
    /// </summary>
    public MatchRating MatchRating { get; init; } = MatchRating.FirstAvailable;

    /// <summary>
    ///   Where the candidate came from, which decides whether an automatic
    ///   search may take it (see <see cref="MetadataAutoLinkOrigin"/>).
    /// </summary>
    public MetadataAutoLinkOrigin Origin { get; init; } = MetadataAutoLinkOrigin.Search;

    /// <summary>
    ///   The rating of the stored link a candidate of another origin was
    ///   listed from, shown for context only, or <c>null</c> for a
    ///   search result.
    /// </summary>
    public MatchRating? LinkMatchRating { get; init; }

    /// <summary>
    ///   The AniDB anime whose link a
    ///   <see cref="MetadataAutoLinkOrigin.PrequelLink"/> candidate is, or
    ///   <c>null</c> for any other.
    /// </summary>
    public int? PrequelAnidbAnimeID { get; init; }

    /// <summary>
    ///   Whether the match was made from data already stored on the server.
    /// </summary>
    public bool IsLocal { get; init; }

    /// <summary>
    ///   Whether the match was made by asking the source.
    /// </summary>
    public bool IsRemote { get; init; }

    /// <summary>
    ///   Why the candidate is not linked, or <c>null</c> for one
    ///   that is.
    /// </summary>
    public MetadataAutoLinkRejection? Rejection { get; init; }
}
