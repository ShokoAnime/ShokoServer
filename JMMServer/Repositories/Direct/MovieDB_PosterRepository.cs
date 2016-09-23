using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class MovieDB_PosterRepository : BaseDirectRepository<MovieDB_Poster, int>
    {
        public MovieDB_PosterRepository()
        {
            
        }

        public static MovieDB_PosterRepository Create()
        {
            return new MovieDB_PosterRepository();
        }
        public MovieDB_Poster GetByOnlineID(string url)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session, url);
            }
        }

        public MovieDB_Poster GetByOnlineID(ISession session, string url)
        {
            MovieDB_Poster cr = session
                .CreateCriteria(typeof(MovieDB_Poster))
                .Add(Restrictions.Eq("URL", url))
                .UniqueResult<MovieDB_Poster>();
            return cr;
        }

        public List<MovieDB_Poster> GetByMovieID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByMovieID(session.Wrap(), id);
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

        public List<MovieDB_Poster> GetAllOriginal()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(MovieDB_Poster))
                    .Add(Restrictions.Eq("ImageSize", Constants.MovieDBImageSize.Original))
                    .List<MovieDB_Poster>();

                return new List<MovieDB_Poster>(objs);
            }
        }

    }
}