using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class Trakt_ShowRepository
    {
        public void Save(Trakt_Show obj)
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

        public Trakt_Show GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<Trakt_Show>(id);
            }
        }

        public Trakt_Show GetByTraktSlug(string slug)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTraktSlug(session, slug);
            }
        }

        public List<Trakt_Show> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAll(session);
            }
        }

        public List<Trakt_Show> GetAll(ISession session)
        {
            var objs = session
                .CreateCriteria(typeof(Trakt_Show))
                .List<Trakt_Show>();

            return new List<Trakt_Show>(objs);
        }

        public Trakt_Show GetByTraktSlug(ISession session, string slug)
        {
            var cr = session
                .CreateCriteria(typeof(Trakt_Show))
                .Add(Restrictions.Eq("TraktID", slug))
                .UniqueResult<Trakt_Show>();
            return cr;
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