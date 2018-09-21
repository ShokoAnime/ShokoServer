using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class TvDB_SeriesRepository : BaseRepository<TvDB_Series, int>
    {
        private PocoIndex<int, TvDB_Series, int> TvDBIDs;

        internal override int SelectKey(TvDB_Series entity) => entity.TvDB_SeriesID;

        internal override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, TvDB_Series, int>(Cache, a => a.SeriesID);

        }

        internal override void ClearIndexes()
        {
            TvDBIDs = null;
        }


        public TvDB_Series GetByTvDBID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.SeriesID == id);
            }
        }
        public Dictionary<int, TvDB_Series> GetByTvDBIDs(IEnumerable<int> ids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ids.ToDictionary(a=>a,a=>TvDBIDs.GetOne(a));
                return Table.Where(a => ids.Contains(a.SeriesID)).ToDictionary(a => a.SeriesID, a => a);
            }
        }
        public Dictionary<int, List<(CrossRef_AniDB_TvDBV2, TvDB_Series)>> GetByAnimeIDsV2(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                return new Dictionary<int, List<(CrossRef_AniDB_TvDBV2, TvDB_Series)>>();
            Dictionary<int, List<CrossRef_AniDB_TvDBV2>> animetvdb = Repo.Instance.CrossRef_AniDB_TvDBV2.GetByAnimeIDs(animeIds).ToDictionary(a=>a.Key,a=>a.Value);
            return animetvdb.ToDictionary(a => a.Key, a => a.Value.Select(b => (b, GetByTvDBID(b.TvDBID))).ToList());
        }

        internal ILookup<int, (CrossRef_AniDB_TvDB, TvDB_Series)> GetByAnimeIDs(int[] animeIds)
        {
            /*
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
                            */

            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));
            if (animeIds.Length == 0)
                return EmptyLookup<int, (CrossRef_AniDB_TvDB, TvDB_Series)>.Instance;

            using (RepoLock.ReaderLock())
                return GetAll().Join(Repo.Instance.CrossRef_AniDB_TvDB.GetAll(), s => s.SeriesID, x => x.AniDBID, (series, xref) => (xref, series)).ToLookup(a => a.xref.AniDBID);
        }
    }
}