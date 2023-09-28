using System.Threading.Tasks;
using Shoko.Models.Client;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;

namespace Shoko.Server.Providers.TMDB;

public static class TmdbExtensions
{
    public static Movie GetMovie(this TMDbClient client, int movieID, MovieMethods methods = MovieMethods.Undefined)
    {
        return Task.Run(async () => await client.GetMovieAsync(movieID, methods)).Result;
    }

    public static SearchContainer<SearchMovie> SearchMovie(this TMDbClient client, string query)
    {
        return Task.Run(async () => await client.SearchMovieAsync(query)).Result;
    }

    public static CL_MovieDBMovieSearch_Response ToContract(this Movie movie)
        => new()
        {
            MovieID = movie.Id,
            MovieName = movie.Title,
            OriginalName = movie.OriginalTitle,
            Overview = movie.Overview,
        };
}
