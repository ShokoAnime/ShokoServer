using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AuthTokensRepository
    {
        public void Save(AuthTokens obj)
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

        public AuthTokens GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AuthTokens>(id);
            }
        }

        public AuthTokens GetByAuthID(int authID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AuthTokens cr = session
                    .CreateCriteria(typeof(AuthTokens))
                    .Add(Restrictions.Eq("AuthID", authID))
                    .UniqueResult<AuthTokens>();
                return cr;
            }
        }

        public List<AuthTokens> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(AuthTokens))
                    .AddOrder(Order.Asc("AuthID"))
                    .List<AuthTokens>();

                return new List<AuthTokens>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AuthTokens cr = GetByID(id);
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
