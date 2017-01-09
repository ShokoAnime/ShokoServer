using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_RecommendationRepository : BaseDirectRepository<SVR_AniDB_Recommendation, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private AniDB_RecommendationRepository()
        {
            
        }
        public static AniDB_RecommendationRepository Create()
        {
            return new AniDB_RecommendationRepository();
        }

        public List<SVR_AniDB_Recommendation> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<SVR_AniDB_Recommendation> GetByAnimeID(ISessionWrapper session, int id)
        {
            var votes = session
                .CreateCriteria(typeof(SVR_AniDB_Recommendation))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<SVR_AniDB_Recommendation>();

            return new List<SVR_AniDB_Recommendation>(votes);
        }

        public SVR_AniDB_Recommendation GetByAnimeIDAndUserID(ISession session, int animeid, int userid)
        {
            return session
                .CreateCriteria(typeof(SVR_AniDB_Recommendation))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("UserID", userid))
                .UniqueResult<SVR_AniDB_Recommendation>();
        }

    }
}