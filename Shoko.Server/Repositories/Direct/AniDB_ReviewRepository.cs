using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_ReviewRepository : BaseDirectRepository<SVR_AniDB_Review, int>
    {
        private AniDB_ReviewRepository()
        {
            
        }

        public static AniDB_ReviewRepository Create()
        {
            return new AniDB_ReviewRepository();
        }
        public SVR_AniDB_Review GetByReviewID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AniDB_Review cr = session
                    .CreateCriteria(typeof(SVR_AniDB_Review))
                    .Add(Restrictions.Eq("ReviewID", id))
                    .UniqueResult<SVR_AniDB_Review>();
                return cr;
            }
        }

    }
}