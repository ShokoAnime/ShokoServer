using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_SeiyuuRepository : BaseDirectRepository<SVR_AniDB_Seiyuu, int>
    {

        private AniDB_SeiyuuRepository()
        {
           
        }

        public static AniDB_SeiyuuRepository Create()
        {
            return new AniDB_SeiyuuRepository();
        }
        public SVR_AniDB_Seiyuu GetBySeiyuuID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AniDB_Seiyuu cr = session
                    .CreateCriteria(typeof(SVR_AniDB_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .UniqueResult<SVR_AniDB_Seiyuu>();
                return cr;
            }
        }

        public SVR_AniDB_Seiyuu GetBySeiyuuID(ISession session, int id)
        {
            SVR_AniDB_Seiyuu cr = session
                .CreateCriteria(typeof(SVR_AniDB_Seiyuu))
                .Add(Restrictions.Eq("SeiyuuID", id))
                .UniqueResult<SVR_AniDB_Seiyuu>();
            return cr;
        }
    }
}