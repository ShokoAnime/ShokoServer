using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_ReviewRepository : BaseDirectRepository<AniDB_Review, int>
    {
        public AniDB_Review GetByReviewID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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