using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.AniDB;
using TMDbLib.Objects.Search;

namespace Shoko.Server.Providers.TMDB;

public class TmdbAutoSearchResult : ITmdbAutoSearchResult
{
    /// <inheritdoc/>
    public bool IsLocal { get; set; } = false;

    /// <inheritdoc/>
    public bool IsRemote { get; set; } = false;

    /// <inheritdoc/>
    public MatchRating MatchRating { get; set; }

    /// <summary>
    ///   The candidate as the matching engine judged it, fetched whole, or
    ///   <see langword="null"/> when it was not fetched.
    /// </summary>
    public MetadataSearchResult? Candidate { get; init; }

    /// <summary>
    ///   Why the auto-search turned the candidate down, or
    ///   <see langword="null"/> when it took it.
    /// </summary>
    public MetadataAutoLinkRejection? Rejection { get; set; }

    /// <summary>
    ///   Where the candidate came from: the search, a link listed for
    ///   context, the anime's AniDB resources or its links on other sources.
    /// </summary>
    public MetadataAutoLinkOrigin Origin { get; init; } = MetadataAutoLinkOrigin.Search;

    /// <summary>
    ///   The rating of the stored link a candidate listed for context came
    ///   from, or <see langword="null"/> for a search result.
    /// </summary>
    public MatchRating? LinkMatchRating { get; init; }

    /// <summary>
    ///   The AniDB anime whose link a prequel's candidate is, or
    ///   <see langword="null"/> for any other.
    /// </summary>
    public int? PrequelAnidbAnimeID { get; init; }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(AnidbEpisode))]
    [MemberNotNullWhen(true, nameof(TmdbMovie))]
    [MemberNotNullWhen(true, nameof(TmdbMovieRaw))]
    [MemberNotNullWhen(false, nameof(TmdbShow))]
    [MemberNotNullWhen(false, nameof(TmdbShowRaw))]
    public bool IsMovie { get; init; }

    /// <inheritdoc/>
    IAnidbAnime ITmdbAutoSearchResult.AnidbAnime => AnidbAnime;

    /// <inheritdoc/>
    IAnidbEpisode? ITmdbAutoSearchResult.AnidbEpisode => AnidbEpisode;

    /// <inheritdoc/>
    ITmdbShowSearchResult? ITmdbAutoSearchResult.TmdbShow => _tmdbShowResult;

    /// <inheritdoc/>
    ITmdbMovieSearchResult? ITmdbAutoSearchResult.TmdbMovie => _tmdbMovieResult;

    /// <summary>
    /// The AniDB anime associated with the search result.
    /// </summary>
    public AniDB_Anime AnidbAnime { get; init; }

    /// <summary>
    /// The AniDB episode associated with the search result, if it's a movie match.
    /// </summary>
    public AniDB_Episode? AnidbEpisode { get; init; }

    /// <summary>
    /// The raw TMDB show search result from TMDbLib, if it's a show match.
    /// </summary>
    public SearchTv? TmdbShowRaw { get; init; }

    /// <summary>
    /// The raw TMDB movie search result from TMDbLib, if it's a movie match.
    /// </summary>
    public SearchMovie? TmdbMovieRaw { get; init; }

    /// <summary>
    /// The TMDB show search result, if it's a show match.
    /// </summary>
    public TmdbShowSearchResult? TmdbShow => _tmdbShowResult;

    /// <summary>
    /// The TMDB movie search result, if it's a movie match.
    /// </summary>
    public TmdbMovieSearchResult? TmdbMovie => _tmdbMovieResult;

    private TmdbShowSearchResult? _tmdbShowResult;
    private TmdbMovieSearchResult? _tmdbMovieResult;

    public TmdbAutoSearchResult(AniDB_Anime anime, SearchTv show, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        IsMovie = false;
        AnidbAnime = anime;
        TmdbShowRaw = show;
        _tmdbShowResult = new TmdbShowSearchResult(show);
        MatchRating = matchRating;
    }

    public TmdbAutoSearchResult(AniDB_Anime anime, AniDB_Episode episode, SearchMovie movie, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        IsMovie = true;
        AnidbAnime = anime;
        AnidbEpisode = episode;
        TmdbMovieRaw = movie;
        _tmdbMovieResult = new TmdbMovieSearchResult(movie);
        MatchRating = matchRating;
    }

    public TmdbAutoSearchResult(TmdbAutoSearchResult result)
    {
        IsMovie = result.IsMovie;
        IsLocal = result.IsLocal;
        IsRemote = result.IsRemote;
        AnidbAnime = result.AnidbAnime;
        AnidbEpisode = result.AnidbEpisode;
        TmdbMovieRaw = result.TmdbMovieRaw;
        TmdbShowRaw = result.TmdbShowRaw;
        _tmdbMovieResult = result._tmdbMovieResult;
        _tmdbShowResult = result._tmdbShowResult;
        MatchRating = result.MatchRating;
        Candidate = result.Candidate;
        Rejection = result.Rejection;
        Origin = result.Origin;
        LinkMatchRating = result.LinkMatchRating;
        PrequelAnidbAnimeID = result.PrequelAnidbAnimeID;
    }
}
