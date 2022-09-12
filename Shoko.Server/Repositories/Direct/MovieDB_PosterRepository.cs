using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDB_PosterRepository : BaseDirectRepository<MovieDB_Poster, int>
    {
        public MovieDB_Poster GetByOnlineID(string url)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session, url);
            }
        }

        public MovieDB_Poster GetByOnlineID(ISession session, string url)
        {
            MovieDB_Poster cr = session
                .CreateCriteria(typeof(MovieDB_Poster))
                .Add(Restrictions.Eq("URL", url))
                .List<MovieDB_Poster>().FirstOrDefault();
            return cr;
        }

        public List<MovieDB_Poster> GetByMovieID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByMovieID(session.Wrap(), id);
            }
        }
        public List<MovieDB_Poster> GetBySeriesID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), id);
            }
        }
        public List<MovieDB_Poster> GetByMovieID(ISessionWrapper session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(MovieDB_Poster))
                .Add(Restrictions.Eq("MovieId", id))
                .List<MovieDB_Poster>();

            return new List<MovieDB_Poster>(objs);
        }
        public List<MovieDB_Poster> GetBySeriesID(ISessionWrapper session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(MovieDB_Poster))
                .Add(Restrictions.Eq("SeriesId", id))
                .List<MovieDB_Poster>();

            return new List<MovieDB_Poster>(objs);
        }
        public List<MovieDB_Poster> GetAllOriginal()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(MovieDB_Poster))
                    .Add(Restrictions.Eq("ImageSize", Shoko.Models.Constants.MovieDBImageSize.Original))
                    .List<MovieDB_Poster>();

                return new List<MovieDB_Poster>(objs);
            }
        }
    }
}