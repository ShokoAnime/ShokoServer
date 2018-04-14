using System;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class TvDB_SeriesRepository : BaseCachedRepository<TvDB_Series, int>
    {
        private PocoIndex<int, TvDB_Series, int> TvDBIDs;

        public override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, TvDB_Series, int>(Cache, a => a.SeriesID);
        }

        private TvDB_SeriesRepository()
        {
        }

        public static TvDB_SeriesRepository Create()
        {
            var repo = new TvDB_SeriesRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        public TvDB_Series GetByTvDBID(int id)
        {
            lock (Cache)
            {
                return TvDBIDs.GetOne(id);
            }
        }

        public ILookup<int, Tuple<CrossRef_AniDB_TvDB, TvDB_Series>> GetByAnimeIDs(ISessionWrapper session,
            int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, Tuple<CrossRef_AniDB_TvDB, TvDB_Series>>.Instance;
            }

            lock (globalDBLock)
            {
                var tvDbSeriesByAnime = session.CreateSQLQuery(@"
                SELECT {cr.*}, {series.*}
                    FROM CrossRef_AniDB_TvDB cr
                        INNER JOIN TvDB_Series series
                            ON series.SeriesID = cr.TvDBID
                    WHERE cr.AniDBID IN (:animeIds)")
                    .AddEntity("cr", typeof(CrossRef_AniDB_TvDB))
                    .AddEntity("series", typeof(TvDB_Series))
                    .SetParameterList("animeIds", animeIds)
                    .List<object[]>()
                    .ToLookup(r => ((CrossRef_AniDB_TvDB) r[0]).AniDBID,
                        r => new Tuple<CrossRef_AniDB_TvDB, TvDB_Series>((CrossRef_AniDB_TvDB) r[0],
                            (TvDB_Series) r[1]));

                return tvDbSeriesByAnime;
            }
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(TvDB_Series entity)
        {
            return entity.TvDB_SeriesID;
        }
    }
}
