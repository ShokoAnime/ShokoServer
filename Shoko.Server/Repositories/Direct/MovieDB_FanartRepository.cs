using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Commons.Collections;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDB_FanartRepository : BaseDirectRepository<MovieDB_Fanart, int>
    {
        public MovieDB_Fanart GetByOnlineID(string url)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session, url);
            }
        }

        public MovieDB_Fanart GetByOnlineID(ISession session, string url)
        {
            MovieDB_Fanart cr = session
                .CreateCriteria(typeof(MovieDB_Fanart))
                .Add(Restrictions.Eq("URL", url))
                .List<MovieDB_Fanart>().FirstOrDefault();
            return cr;
        }

        public List<MovieDB_Fanart> GetByMovieID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByMovieID(session.Wrap(), id);
            }
        }
        public List<MovieDB_Fanart> GetBySeriesID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), id);
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
        public List<MovieDB_Fanart> GetBySeriesID(ISessionWrapper session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(MovieDB_Fanart))
                .Add(Restrictions.Eq("SeriesId", id))
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


            var fanartByAnimeMovies = session.CreateSQLQuery(@"
                SELECT DISTINCT adbOther.AnimeID, {mdbFanart.*}
                    FROM CrossRef_AniDB AS adbOther
                        INNER JOIN MovieDB_Fanart AS mdbFanart
                            ON mdbFanart.MovieId = adbOther.CrossRefID
                    WHERE adbOther.CrossRefType = :crossRefType AND 
                    adbOther.ProviderMediaType = :mediaType AND adbOther.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("mdbFanart", typeof(MovieDB_Fanart))
                .SetInt32("crossRefTypeMovie", (int) CrossRefType.MovieDB)
                .SetInt32("mediaType", (int)MediaType.Movie)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int) r[0], r => (MovieDB_Fanart) r[1]);
            var fanartByAnimeSeries = session.CreateSQLQuery(@"
                SELECT DISTINCT adbOther.AnimeID, {mdbFanart.*}
                    FROM CrossRef_AniDB AS adbOther
                        INNER JOIN MovieDB_Fanart AS mdbFanart
                            ON mdbFanart.SeriesId = adbOther.CrossRefID
                    WHERE adbOther.CrossRefType = :crossRefType AND 
                    adbOther.ProviderMediaType = :mediaType AND adbOther.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("mdbFanart", typeof(MovieDB_Fanart))
                .SetInt32("crossRefTypeMovie", (int)CrossRefType.MovieDB)
                .SetInt32("mediaType", (int)MediaType.TvShow)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (MovieDB_Fanart)r[1]);
            return fanartByAnimeSeries.Union(fanartByAnimeMovies);
        }

        public List<MovieDB_Fanart> GetAllOriginal()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(MovieDB_Fanart))
                    .Add(Restrictions.Eq("ImageSize", Shoko.Models.Constants.MovieDBImageSize.Original))
                    .List<MovieDB_Fanart>();

                return new List<MovieDB_Fanart>(objs);
            }
        }
    }
}