using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

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
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.SeriesID == id);
            }
        }
        public Dictionary<int, TvDB_Series> GetByTvDBIDs(IEnumerable<int> ids)
        {
            using (CacheLock.ReaderLock())
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
            Dictionary<int, List<CrossRef_AniDB_TvDBV2>> animetvdb = Repo.CrossRef_AniDB_TvDBV2.GetByAnimeIDs(animeIds).ToDictionary(a=>a.Key,a=>a.Value);
            return animetvdb.ToDictionary(a => a.Key, a => a.Value.Select(b => (b, GetByTvDBID(b.TvDBID))).ToList());
        }


    }
}