using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class TvDB_SeriesRepository : BaseDirectRepository<TvDB_Series, int>
    {
        private TvDB_SeriesRepository()
        {
            
        }

        public static TvDB_SeriesRepository Create()
        {
            return new TvDB_SeriesRepository();
        }
        public TvDB_Series GetByTvDBID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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