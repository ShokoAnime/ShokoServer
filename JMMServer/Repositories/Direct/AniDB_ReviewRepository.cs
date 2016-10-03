using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
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