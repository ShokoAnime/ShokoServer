using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class TvDB_SeriesRepository : BaseDirectRepository<TvDB_Series, int>
    {

        public TvDB_Series GetByTvDBID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTvDBID(session.Wrap(), id);
            }
        }

        public TvDB_Series GetByTvDBID(ISessionWrapper session, int id)
        {
            return session
                .CreateCriteria(typeof(TvDB_Series))
                .Add(Restrictions.Eq("SeriesID", id))
                .UniqueResult<TvDB_Series>();
        }

    }
}