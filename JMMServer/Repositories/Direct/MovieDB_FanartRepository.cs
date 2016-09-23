using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class MovieDB_FanartRepository : BaseDirectRepository<MovieDB_Fanart, int>
    {
        public MovieDB_FanartRepository()
        {
            
        }

        public static MovieDB_FanartRepository Create()
        {
            return new MovieDB_FanartRepository();
        }
        public MovieDB_Fanart GetByOnlineID(string url)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session, url);
            }
        }

        public MovieDB_Fanart GetByOnlineID(ISession session, string url)
        {
            MovieDB_Fanart cr = session
                .CreateCriteria(typeof(MovieDB_Fanart))
                .Add(Restrictions.Eq("URL", url))
                .UniqueResult<MovieDB_Fanart>();
            return cr;
        }

        public List<MovieDB_Fanart> GetByMovieID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByMovieID(session.Wrap(), id);
            }
        }

        public List<MovieDB_Fanart> GetByMovieID(ISessionWrapper session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(MovieDB_Fanart))
                .Add(Restrictions.Eq("MovieId", id))
                .List<MovieDB_Fanart>();

            return new List<MovieDB_Fanart>(objs);
        }

        public ILookup<int, MovieDB_Fanart> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, MovieDB_Fanart>.Instance;
            }

            var fanartByAnime = session.CreateSQLQuery(@"
                SELECT DISTINCT adbOther.AnimeID, {mdbFanart.*}
                    FROM CrossRef_AniDB_Other AS adbOther
                        INNER JOIN MovieDB_Fanart AS mdbFanart
                            ON mdbFanart.MovieId = adbOther.CrossRefID
                    WHERE adbOther.CrossRefType = :crossRefType AND adbOther.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("mdbFanart", typeof(MovieDB_Fanart))
                .SetInt32("crossRefType", (int)CrossRefType.MovieDB)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (MovieDB_Fanart)r[1]);

            return fanartByAnime;
        }

        public List<MovieDB_Fanart> GetAllOriginal()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(MovieDB_Fanart))
                    .Add(Restrictions.Eq("ImageSize", Constants.MovieDBImageSize.Original))
                    .List<MovieDB_Fanart>();

                return new List<MovieDB_Fanart>(objs);
            }
        }
    }
}