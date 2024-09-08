
using System.Diagnostics.CodeAnalysis;
using Shoko.Server.Models;
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

    [MemberNotNullWhen(true, nameof(AnidbEpisode))]
    [MemberNotNullWhen(true, nameof(TmdbMovie))]
    [MemberNotNullWhen(false, nameof(TmdbShow))]
    public bool IsMovie { get; init; }

    public SVR_AniDB_Anime AnidbAnime { get; init; }

    public SVR_AniDB_Episode? AnidbEpisode { get; init; }

    public SearchTv? TmdbShow { get; init; }

    public SearchMovie? TmdbMovie { get; init; }

    public TmdbAutoSearchResult(SVR_AniDB_Anime anime, SearchTv show)
    {
        IsMovie = false;
        AnidbAnime = anime;
        TmdbShow = show;
    }

    public TmdbAutoSearchResult(SVR_AniDB_Anime anime, SVR_AniDB_Episode episode, SearchMovie movie)
    {
        IsMovie = true;
        AnidbAnime = anime;
        AnidbEpisode = episode;
        TmdbMovie = movie;
    }

    public TmdbAutoSearchResult(TmdbAutoSearchResult result)
    {
        AnidbAnime = result.AnidbAnime;
        AnidbEpisode = result.AnidbEpisode;
        TmdbMovie = result.TmdbMovie;
        TmdbShow = result.TmdbShow;
    }
}
