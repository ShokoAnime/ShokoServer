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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_Seiyuu cr = session
                    .CreateCriteria(typeof(AniDB_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .UniqueResult<AniDB_Seiyuu>();
                return cr;
            }
        }

        public AniDB_Seiyuu GetBySeiyuuID(ISession session, int id)
        {
            AniDB_Seiyuu cr = session
                .CreateCriteria(typeof(AniDB_Seiyuu))
                .Add(Restrictions.Eq("SeiyuuID", id))
                .UniqueResult<AniDB_Seiyuu>();
            return cr;
        }
    }
}