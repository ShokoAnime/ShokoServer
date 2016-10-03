using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class TvDB_ImagePosterRepository : BaseDirectRepository<TvDB_ImagePoster, int>
    {
        private TvDB_ImagePosterRepository()
        {
            
        }

        public static TvDB_ImagePosterRepository Create()
        {
            return new TvDB_ImagePosterRepository();
        }
        public TvDB_ImagePoster GetByTvDBID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                TvDB_ImagePoster cr = session
                    .CreateCriteria(typeof(TvDB_ImagePoster))
                    .Add(Restrictions.Eq("Id", id))
                    .UniqueResult<TvDB_ImagePoster>();
                return cr;
            }
        }


        public List<TvDB_ImagePoster> GetBySeriesID(int seriesID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), seriesID);
            }
        }

        public List<TvDB_ImagePoster> GetBySeriesID(ISessionWrapper session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_ImagePoster))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_ImagePoster>();

            return new List<TvDB_ImagePoster>(objs);
        }

    }
}