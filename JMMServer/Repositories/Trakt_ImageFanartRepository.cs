using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class Trakt_ImageFanartRepository
    {
        public void Save(Trakt_ImageFanart obj)
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

        public Trakt_ImageFanart GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session, id);
            }
        }

        public Trakt_ImageFanart GetByID(ISession session, int id)
        {
            return session.Get<Trakt_ImageFanart>(id);
        }


        public List<Trakt_ImageFanart> GetByShowID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByShowID(session, id);
            }
        }

        public List<Trakt_ImageFanart> GetByShowID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(Trakt_ImageFanart))
                .Add(Restrictions.Eq("Trakt_ShowID", id))
                .List<Trakt_ImageFanart>();

            return new List<Trakt_ImageFanart>(objs);
        }

        public Trakt_ImageFanart GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var obj = session
                    .CreateCriteria(typeof(Trakt_ImageFanart))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .UniqueResult<Trakt_ImageFanart>();

                return obj;
            }
        }

        public List<Trakt_ImageFanart> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_ImageFanart))
                    .List<Trakt_ImageFanart>();

                return new List<Trakt_ImageFanart>(objs);
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