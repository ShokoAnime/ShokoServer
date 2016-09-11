using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class TvDB_ImageWideBannerRepository
    {
        public void Save(TvDB_ImageWideBanner obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public TvDB_ImageWideBanner GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session.Wrap(), id);
            }
        }

        public TvDB_ImageWideBanner GetByID(ISessionWrapper session, int id)
        {
            return session.Get<TvDB_ImageWideBanner>(id);
        }

        public TvDB_ImageWideBanner GetByTvDBID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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
            using (var session = JMMService.SessionFactory.OpenSession())
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

        public List<TvDB_ImageWideBanner> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_ImageWideBanner))
                    .List<TvDB_ImageWideBanner>();

                return new List<TvDB_ImageWideBanner>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    TvDB_ImageWideBanner cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}