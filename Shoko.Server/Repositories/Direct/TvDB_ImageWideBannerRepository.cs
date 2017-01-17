using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class TvDB_ImageWideBannerRepository : BaseDirectRepository<TvDB_ImageWideBanner, int>
    {
        private TvDB_ImageWideBannerRepository()
        {
            
        }

        public static TvDB_ImageWideBannerRepository Create()
        {
            return new TvDB_ImageWideBannerRepository();
        }

        public TvDB_ImageWideBanner GetByTvDBID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                TvDB_ImageWideBanner cr = session
                    .CreateCriteria(typeof(TvDB_ImageWideBanner))
                    .Add(Restrictions.Eq("Id", id))
                    .UniqueResult<TvDB_ImageWideBanner>();
                return cr;
            }
        }

        public List<TvDB_ImageWideBanner> GetBySeriesID(int seriesID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session, seriesID);
            }
        }

        public List<TvDB_ImageWideBanner> GetBySeriesID(ISession session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_ImageWideBanner))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_ImageWideBanner>();

            return new List<TvDB_ImageWideBanner>(objs);
        }

        public ILookup<int, TvDB_ImageWideBanner> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, TvDB_ImageWideBanner>.Instance;
            }

            var bannersByAnime = session.CreateSQLQuery(@"
                SELECT DISTINCT crAdbTvTb.AnimeID, {tvdbBanner.*}
                   FROM CrossRef_AniDB_TvDBV2 AS crAdbTvTb
                      INNER JOIN TvDB_ImageWideBanner AS tvdbBanner
                         ON tvdbBanner.SeriesID = crAdbTvTb.TvDBID
                   WHERE crAdbTvTb.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("tvdbBanner", typeof(TvDB_ImageWideBanner))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (TvDB_ImageWideBanner)r[1]);

            return bannersByAnime;
        }

    }
}