﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class MovieDb_MovieRepository : BaseDirectRepository<MovieDB_Movie, int>
{
    public MovieDB_Movie GetByOnlineID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByOnlineID(session.Wrap(), id);
        }
    }

    public MovieDB_Movie GetByOnlineID(ISessionWrapper session, int id)
    {
        lock (GlobalDBLock)
        {
            var cr = session
                .CreateCriteria(typeof(MovieDB_Movie))
                .Add(Restrictions.Eq("MovieId", id))
                .UniqueResult<MovieDB_Movie>();
            return cr;
        }
    }

    public Dictionary<int, Tuple<CrossRef_AniDB_Other, MovieDB_Movie>> GetByAnimeIDs(ISessionWrapper session,
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
            return new Dictionary<int, Tuple<CrossRef_AniDB_Other, MovieDB_Movie>>();
        }

        lock (GlobalDBLock)
        {
            var movieByAnime = session.CreateSQLQuery(
                    @"
                SELECT {cr.*}, {movie.*}
                    FROM CrossRef_AniDB_Other cr
                        INNER JOIN MovieDB_Movie movie
                            ON cr.CrossRefType = :crossRefType
                                AND movie.MovieId = cr.CrossRefID
                    WHERE cr.AnimeID IN (:animeIds)"
                )
                .AddEntity("cr", typeof(CrossRef_AniDB_Other))
                .AddEntity("movie", typeof(MovieDB_Movie))
                .SetInt32("crossRefType", (int)CrossRefType.MovieDB)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToDictionary(
                    r => ((CrossRef_AniDB_Other)r[0]).AnimeID,
                    r => new Tuple<CrossRef_AniDB_Other, MovieDB_Movie>(
                        (CrossRef_AniDB_Other)r[0],
                        (MovieDB_Movie)r[1]
                    )
                );

            return movieByAnime;
        }
    }
}
