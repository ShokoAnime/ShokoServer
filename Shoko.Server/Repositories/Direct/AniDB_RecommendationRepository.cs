using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_RecommendationRepository : BaseDirectRepository<AniDB_Recommendation, int>
    {
        public List<AniDB_Recommendation> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
    }
}