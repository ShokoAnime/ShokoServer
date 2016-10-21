using System;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
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

        public ILookup<int, Tuple<CrossRef_AniDB_TvDBV2, TvDB_Series>> GetByAnimeIDsV2(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, Tuple<CrossRef_AniDB_TvDBV2, TvDB_Series>>.Instance;
            }

            var tvDbSeriesByAnime = session.CreateSQLQuery(@"
                SELECT {cr.*}, {series.*}
                    FROM CrossRef_AniDB_TvDBV2 cr
                        INNER JOIN TvDB_Series series
                            ON series.SeriesID = cr.TvDBID
                    WHERE cr.AnimeID IN (:animeIds)")
                .AddEntity("cr", typeof(CrossRef_AniDB_TvDBV2))
                .AddEntity("series", typeof(TvDB_Series))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => ((CrossRef_AniDB_TvDBV2)r[0]).AnimeID,
                    r => new Tuple<CrossRef_AniDB_TvDBV2, TvDB_Series>((CrossRef_AniDB_TvDBV2)r[0], (TvDB_Series)r[1]));

            return tvDbSeriesByAnime;
        }
    }
}