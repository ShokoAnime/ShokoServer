using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   One show or movie the auto-search judged for an anime, or for one of
///   its episodes, taken or turned down.
/// </summary>
internal sealed class TmdbAutoSearchResult
{
    /// <summary>
    ///   The anime searched for.
    /// </summary>
    public required IAnidbAnime AnidbAnime { get; init; }

    /// <summary>
    ///   The episode a movie stands for, for a movie.
    /// </summary>
    public IAnidbEpisode? AnidbEpisode { get; init; }

    /// <summary>
    ///   The show or movie, as it was judged.
    /// </summary>
    public required MetadataSearchResult Candidate { get; init; }

    /// <summary>
    ///   Whether it is a movie rather than a show.
    /// </summary>
    public bool IsMovie => Candidate is MetadataMovieSearchResult;

    /// <summary>
    ///   Its TMDb ID.
    /// </summary>
    public int TmdbID => Candidate.ID.TryGetNumericID<int>(out var id) ? id : 0;

    /// <summary>
    ///   How well it matched.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    /// <summary>
    ///   Whether it was read from the store.
    /// </summary>
    public bool IsLocal { get; init; }

    /// <summary>
    ///   Whether it was fetched from TMDb.
    /// </summary>
    public bool IsRemote { get; init; }

    /// <summary>
    ///   Why it was turned down, or <c>null</c> when it was taken.
    /// </summary>
    public MetadataAutoLinkRejection? Rejection { get; set; }

    /// <summary>
    ///   Where it came from.
    /// </summary>
    public MetadataAutoLinkOrigin Origin { get; init; } = MetadataAutoLinkOrigin.Search;

    /// <summary>
    ///   The rating of the link it stands for, when listed for context.
    /// </summary>
    public MatchRating? LinkMatchRating { get; init; }

    /// <summary>
    ///   The prequel whose link it stands for, when listed for context.
    /// </summary>
    public int? PrequelAnidbAnimeID { get; init; }

    /// <summary>
    ///   The candidate, as the core takes it.
    /// </summary>
    /// <returns>The candidate.</returns>
    public MetadataAutoLinkCandidate ToCandidate()
        => new()
        {
            Result = Candidate,
            AnidbAnimeID = AnidbAnime.AnidbID,
            AnidbEpisodeID = IsMovie ? AnidbEpisode?.AnidbID : null,
            MatchRating = MatchRating,
            IsLocal = IsLocal,
            IsRemote = IsRemote,
            Rejection = Rejection,
            Origin = Origin,
            LinkMatchRating = LinkMatchRating,
            PrequelAnidbAnimeID = PrequelAnidbAnimeID,
        };
}
