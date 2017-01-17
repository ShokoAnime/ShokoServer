using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_ReviewRepository : BaseDirectRepository<AniDB_Review, int>
    {
        private AniDB_ReviewRepository()
        {
            
        }

        public static AniDB_ReviewRepository Create()
        {
            return new AniDB_ReviewRepository();
        }
        public AniDB_Review GetByReviewID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_Review cr = session
                    .CreateCriteria(typeof(AniDB_Review))
                    .Add(Restrictions.Eq("ReviewID", id))
                    .UniqueResult<AniDB_Review>();
                return cr;
            }
        }

    }
}