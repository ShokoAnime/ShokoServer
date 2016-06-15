using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class Trakt_FriendRepository
    {
        public void Save(Trakt_Friend obj)
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

        public Trakt_Friend GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<Trakt_Friend>(id);
            }
        }

        public Trakt_Friend GetByUsername(string username)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByUsername(session, username);
            }
        }

        public Trakt_Friend GetByUsername(ISession session, string username)
        {
            Trakt_Friend obj = session
                .CreateCriteria(typeof(Trakt_Friend))
                .Add(Restrictions.Eq("Username", username))
                .UniqueResult<Trakt_Friend>();

            return obj;
        }

        public List<Trakt_Friend> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Friend))
                    .List<Trakt_Friend>();

                return new List<Trakt_Friend>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Trakt_Friend cr = GetByID(id);
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