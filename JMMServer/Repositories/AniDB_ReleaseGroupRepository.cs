using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
    public class AniDB_ReleaseGroupRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Save(AniDB_ReleaseGroup obj)
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

        public AniDB_ReleaseGroup GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_ReleaseGroup>(id);
            }
        }

        public AniDB_ReleaseGroup GetByGroupID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_ReleaseGroup cr = session
                    .CreateCriteria(typeof(AniDB_ReleaseGroup))
                    .Add(Restrictions.Eq("GroupID", id))
                    .UniqueResult<AniDB_ReleaseGroup>();
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
                    AniDB_ReleaseGroup cr = GetByID(id);
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