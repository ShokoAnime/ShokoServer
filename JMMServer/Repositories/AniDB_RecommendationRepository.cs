using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
    public class AniDB_RecommendationRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Save(AniDB_Recommendation obj)
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
        public void Save(IEnumerable<AniDB_Recommendation> objs)
        {
            if (!objs.Any())
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    foreach(AniDB_Recommendation obj in objs)
                        session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }
        public AniDB_Recommendation GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Recommendation>(id);
            }
        }

        public List<AniDB_Recommendation> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<AniDB_Recommendation> GetByAnimeID(ISessionWrapper session, int id)
        {
            var votes = session
                .CreateCriteria(typeof(AniDB_Recommendation))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Recommendation>();

            return new List<AniDB_Recommendation>(votes);
        }

        public AniDB_Recommendation GetByAnimeIDAndUserID(ISession session, int animeid, int userid)
        {
            return session
                .CreateCriteria(typeof(AniDB_Recommendation))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("UserID", userid))
                .UniqueResult<AniDB_Recommendation>();
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Recommendation cr = GetByID(id);
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