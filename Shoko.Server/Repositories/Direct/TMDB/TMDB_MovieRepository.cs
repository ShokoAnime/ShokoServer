using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_MovieRepository : BaseDirectRepository<TMDB_Movie, int>
{
    public TMDB_Movie? GetByTmdbMovieID(int tmdbMovieId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie>()
                .Where(a => a.TmdbMovieID == tmdbMovieId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public IReadOnlyList<TMDB_Movie> GetByTmdbCollectionID(int tmdbCollectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie>()
                .Where(a => a.TmdbCollectionID == tmdbCollectionId)
                .OrderBy(a => a.EnglishTitle)
                .ThenBy(a => a.TmdbMovieID)
                .ToList();
        });
    }

    public Dictionary<int, Tuple<CrossRef_AniDB_TMDB_Movie, TMDB_Movie>> GetByAnimeIDs(ISessionWrapper session,
        int[] animeIds)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));
        ArgumentNullException.ThrowIfNull(animeIds, nameof(animeIds));

        if (animeIds.Length == 0)
            return [];

        return Lock(() =>
        {
            var movieByAnime = session.CreateSQLQuery(
                    @"
                SELECT {cr.*}, {movie.*}
                    FROM CrossRef_AniDB_TMDB_Movie cr
                        INNER JOIN TMDB_Movie movie
                            ON movie.TmdbMovieID = cr.TmdbMovieID
                    WHERE cr.AnidbAnimeID IN (:animeIds)"
                )
                .AddEntity("cr", typeof(CrossRef_AniDB_TMDB_Movie))
                .AddEntity("movie", typeof(TMDB_Movie))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToDictionary(
                    r => ((CrossRef_AniDB_TMDB_Movie)r[0]).AnidbAnimeID,
                    r => new Tuple<CrossRef_AniDB_TMDB_Movie, TMDB_Movie>(
                        (CrossRef_AniDB_TMDB_Movie)r[0],
                        (TMDB_Movie)r[1]
                    )
                );

            return movieByAnime;
        });
    }

    public TMDB_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
