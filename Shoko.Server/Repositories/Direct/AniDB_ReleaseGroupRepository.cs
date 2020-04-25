using NHibernate.Criterion;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_ReleaseGroupRepository : BaseDirectRepository<AniDB_ReleaseGroup, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private AniDB_ReleaseGroupRepository()
        {
        }

        public static AniDB_ReleaseGroupRepository Create()
        {
            return new AniDB_ReleaseGroupRepository();
        }

        public AniDB_ReleaseGroup GetByGroupID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_ReleaseGroup cr = session
                    .CreateCriteria(typeof(AniDB_ReleaseGroup))
                    .Add(Restrictions.Eq("GroupID", id))
                    .UniqueResult<AniDB_ReleaseGroup>();
                return cr;
            }
        }
    }
}