using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Enums;
using Shoko.Server.Models.AniDB;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbAutoSearchResult
{
    /// <summary>
    /// Indicates that this is a local match using existing data instead of a
    /// remote match.
    /// </summary>
    public bool IsLocal { get; set; } = false;

    /// <summary>
    /// Indicates that this is a remote match.
    /// </summary>
    public bool IsRemote { get; set; } = false;

    /// <summary>
    /// The match rating of the result.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    [MemberNotNullWhen(true, nameof(AnidbEpisode))]
    [MemberNotNullWhen(true, nameof(TmdbMovie))]
    [MemberNotNullWhen(false, nameof(TmdbShow))]
    public bool IsMovie { get; init; }

    public AniDB_Anime AnidbAnime { get; init; }

    public AniDB_Episode? AnidbEpisode { get; init; }

    public SearchTv? TmdbShow { get; init; }

    public SearchMovie? TmdbMovie { get; init; }

    public TmdbAutoSearchResult(AniDB_Anime anime, SearchTv show, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        IsMovie = false;
        AnidbAnime = anime;
        TmdbShow = show;
        MatchRating = matchRating;
    }

    public TmdbAutoSearchResult(AniDB_Anime anime, AniDB_Episode episode, SearchMovie movie, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        IsMovie = true;
        AnidbAnime = anime;
        AnidbEpisode = episode;
        TmdbMovie = movie;
        MatchRating = matchRating;
    }

    public TmdbAutoSearchResult(TmdbAutoSearchResult result)
    {
        AnidbAnime = result.AnidbAnime;
        AnidbEpisode = result.AnidbEpisode;
        TmdbMovie = result.TmdbMovie;
        TmdbShow = result.TmdbShow;
        MatchRating = result.MatchRating;
    }
}
