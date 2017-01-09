using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDB_PosterRepository : BaseDirectRepository<MovieDB_Poster, int>
    {
        private MovieDB_PosterRepository()
        {
            
        }

        public static MovieDB_PosterRepository Create()
        {
            return new MovieDB_PosterRepository();
        }
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
                .UniqueResult<MovieDB_Poster>();
            return cr;
        }

        public List<MovieDB_Poster> GetByMovieID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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