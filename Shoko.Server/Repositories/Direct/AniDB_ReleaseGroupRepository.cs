using Shoko.Models.Server;
using NHibernate.Criterion;
using NLog;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_ReleaseGroupRepository : BaseDirectRepository<SVR_AniDB_ReleaseGroup, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private AniDB_ReleaseGroupRepository()
        { }

        public static AniDB_ReleaseGroupRepository Create()
        {
            return new AniDB_ReleaseGroupRepository();
        }
        public SVR_AniDB_ReleaseGroup GetByGroupID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AniDB_ReleaseGroup cr = session
                    .CreateCriteria(typeof(SVR_AniDB_ReleaseGroup))
                    .Add(Restrictions.Eq("GroupID", id))
                    .UniqueResult<SVR_AniDB_ReleaseGroup>();
                return cr;
            }
        }

    }
}