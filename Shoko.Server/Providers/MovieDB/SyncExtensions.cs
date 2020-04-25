using System.Threading.Tasks;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers.MovieDB
{
    public static class SyncExtensions
    {
        public static Movie GetMovie(this TMDbClient client, int movieID, MovieMethods methods = MovieMethods.Undefined)
        {
            return Task.Run(async () => await client.GetMovieAsync(movieID, methods)).Result;
        }

        public static ImagesWithId GetMovieImages(this TMDbClient client, int movieID)
        {
            return Task.Run(async () => await client.GetMovieImagesAsync(movieID)).Result;
        }

        public static TvShow GetTvShow(this TMDbClient client, int movieID, TvShowMethods method,
            string language = null)
        {
            return Task.Run(async () => await client.GetTvShowAsync(movieID, method, language)).Result;
        }

        public static SearchContainer<SearchMovie> SearchMovie(this TMDbClient client, string criteria)
        {
            return Task.Run(async () => await client.SearchMovieAsync(criteria)).Result;
        }
    }
}