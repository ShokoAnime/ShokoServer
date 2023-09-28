using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class MovieDb_MovieRepository : BaseDirectRepository<MovieDB_Movie, int>
{
    public MovieDB_Movie GetByOnlineID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByOnlineIDUnsafe(session.Wrap(), id);
        });
    }

    public MovieDB_Movie GetByOnlineID(ISessionWrapper session, int id)
    {
        return Lock(() => GetByOnlineIDUnsafe(session, id));
    }

    private static MovieDB_Movie GetByOnlineIDUnsafe(ISessionWrapper session, int id)
    {
        var cr = session
            .CreateCriteria(typeof(MovieDB_Movie))
            .Add(Restrictions.Eq("MovieId", id))
            .UniqueResult<MovieDB_Movie>();
        return cr;
    }

    public Dictionary<int, Tuple<CrossRef_AniDB_TMDB_Movie, MovieDB_Movie>> GetByAnimeIDs(ISessionWrapper session,
        int[] animeIds)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeIds == null)
        {
            throw new ArgumentNullException(nameof(animeIds));
        }

        if (animeIds.Length == 0)
        {
            return new Dictionary<int, Tuple<CrossRef_AniDB_TMDB_Movie, MovieDB_Movie>>();
        }

        return Lock(() =>
        {
            var movieByAnime = session.CreateSQLQuery(
                    @"
                SELECT {cr.*}, {movie.*}
                    FROM CrossRef_AniDB_TMDB_Movie cr
                        INNER JOIN MovieDB_Movie movie
                            ON movie.MovieId = cr.TmdbMovieID
                    WHERE cr.AnidbAnimeID IN (:animeIds)"
                )
                .AddEntity("cr", typeof(CrossRef_AniDB_TMDB_Movie))
                .AddEntity("movie", typeof(MovieDB_Movie))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToDictionary(
                    r => ((CrossRef_AniDB_TMDB_Movie)r[0]).AnidbAnimeID,
                    r => new Tuple<CrossRef_AniDB_TMDB_Movie, MovieDB_Movie>(
                        (CrossRef_AniDB_TMDB_Movie)r[0],
                        (MovieDB_Movie)r[1]
                    )
                );

            return movieByAnime;
        });
    }
}
