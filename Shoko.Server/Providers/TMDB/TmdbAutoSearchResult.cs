
using System.Diagnostics.CodeAnalysis;
using Shoko.Server.Models;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbAutoSearchResult
{
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
}
