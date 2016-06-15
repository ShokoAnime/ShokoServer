using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class Trakt_ImagePosterRepository
    {
        public void Save(Trakt_ImagePoster obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public Trakt_ImagePoster GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session, id);
            }
        }

        public Trakt_ImagePoster GetByID(ISession session, int id)
        {
            return session.Get<Trakt_ImagePoster>(id);
        }

        public List<Trakt_ImagePoster> GetByShowID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByShowID(session, id);
            }
        }

        public List<Trakt_ImagePoster> GetByShowID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(Trakt_ImagePoster))
                .Add(Restrictions.Eq("Trakt_ShowID", id))
                .List<Trakt_ImagePoster>();

            return new List<Trakt_ImagePoster>(objs);
        }

        public Trakt_ImagePoster GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Trakt_ImagePoster obj = session
                    .CreateCriteria(typeof(Trakt_ImagePoster))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .UniqueResult<Trakt_ImagePoster>();

                return obj;
            }
        }

        public List<Trakt_ImagePoster> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_ImagePoster))
                    .List<Trakt_ImagePoster>();

                return new List<Trakt_ImagePoster>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Trakt_ImagePoster cr = GetByID(id);
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