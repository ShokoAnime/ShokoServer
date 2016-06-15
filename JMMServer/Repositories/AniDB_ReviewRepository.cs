using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_ReviewRepository
    {
        public void Save(AniDB_Review obj)
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

        public AniDB_Review GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Review>(id);
            }
        }

        public AniDB_Review GetByReviewID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Review cr = session
                    .CreateCriteria(typeof(AniDB_Review))
                    .Add(Restrictions.Eq("ReviewID", id))
                    .UniqueResult<AniDB_Review>();
                return cr;
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Review cr = GetByID(id);
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