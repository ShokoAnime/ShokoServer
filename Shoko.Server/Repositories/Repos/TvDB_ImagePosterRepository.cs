using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class TvDB_ImagePosterRepository : BaseRepository<TvDB_ImagePoster, int>
    {
        private PocoIndex<int, TvDB_ImagePoster, int> SeriesIDs;
        private PocoIndex<int, TvDB_ImagePoster, int> TvDBIDs;

        internal override void PopulateIndexes()
        {
            SeriesIDs = new PocoIndex<int, TvDB_ImagePoster, int>(Cache, a => a.SeriesID);
            TvDBIDs = new PocoIndex<int, TvDB_ImagePoster, int>(Cache, a => a.Id);
        }

        internal override void ClearIndexes()
        {
            SeriesIDs = null;
            TvDBIDs = null;
        }


        internal override int SelectKey(TvDB_ImagePoster entity) => entity.TvDB_ImagePosterID;



        public TvDB_ImagePoster GetByTvDBID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetOne(id);
                return Table.FirstOrDefault(a=>a.Id==id);
            }
        }

        public List<TvDB_ImagePoster> GetBySeriesID(int seriesID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID);
                return Table.Where(a => a.SeriesID == seriesID).ToList();
            }
        }
    }
}