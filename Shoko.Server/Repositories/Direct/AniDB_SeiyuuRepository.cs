using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_SeiyuuRepository : BaseDirectRepository<AniDB_Seiyuu, int>
    {
        private AniDB_SeiyuuRepository()
        {
        }

        public static AniDB_SeiyuuRepository Create()
        {
            return new AniDB_SeiyuuRepository();
        }

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