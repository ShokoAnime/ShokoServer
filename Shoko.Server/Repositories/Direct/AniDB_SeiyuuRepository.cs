using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_SeiyuuRepository : BaseDirectRepository<AniDB_Seiyuu, int>
    {
        public AniDB_Seiyuu GetBySeiyuuID(int id)
        {
            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var cr = session
                    .CreateCriteria(typeof(AniDB_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .UniqueResult<AniDB_Seiyuu>();
                return cr;
            }
        }

        public AniDB_Seiyuu GetBySeiyuuID(ISession session, int id)
        {
            lock (GlobalDBLock)
            {
                var cr = session
                    .CreateCriteria(typeof(AniDB_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .UniqueResult<AniDB_Seiyuu>();
                return cr;
            }
        }
    }
}