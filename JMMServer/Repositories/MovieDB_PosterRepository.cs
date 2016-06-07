using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class MovieDB_PosterRepository
    {
        public void Save(MovieDB_Poster obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Save(session, obj);
            }
        }

        public void Save(ISession session, MovieDB_Poster obj)
        {
            // populate the database
            using (var transaction = session.BeginTransaction())
            {
                session.SaveOrUpdate(obj);
                transaction.Commit();
            }
        }

        public MovieDB_Poster GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session, id);
            }
        }

        public MovieDB_Poster GetByID(ISession session, int id)
        {
            return session.Get<MovieDB_Poster>(id);
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
            var cr = session
                .CreateCriteria(typeof(MovieDB_Poster))
                .Add(Restrictions.Eq("URL", url))
                .UniqueResult<MovieDB_Poster>();
            return cr;
        }

        public List<MovieDB_Poster> GetByMovieID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByMovieID(session, id);
            }
        }

        public List<MovieDB_Poster> GetByMovieID(ISession session, int id)
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

        public List<MovieDB_Poster> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(MovieDB_Poster))
                    .List<MovieDB_Poster>();

                return new List<MovieDB_Poster>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}