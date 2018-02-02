using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class TvDB_ImageWideBannerRepository : BaseRepository<TvDB_ImageWideBanner, int>
    {
        private PocoIndex<int, TvDB_ImageWideBanner, int> SeriesIDs;
        private PocoIndex<int, TvDB_ImageWideBanner, int> TvDBIDs;

        internal override void PopulateIndexes()
        {
            SeriesIDs = new PocoIndex<int, TvDB_ImageWideBanner, int>(Cache, a => a.SeriesID);
            TvDBIDs = new PocoIndex<int, TvDB_ImageWideBanner, int>(Cache, a => a.Id);
        }

        internal override void ClearIndexes()
        {
            SeriesIDs = null;
            TvDBIDs = null;
        }


        internal override int SelectKey(TvDB_ImageWideBanner entity) => entity.TvDB_ImageWideBannerID;





        public TvDB_ImageWideBanner GetByTvDBID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.Id == id);
            }
        }

        public List<TvDB_ImageWideBanner> GetBySeriesID(int seriesID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID);
                return Table.Where(a => a.SeriesID == seriesID).ToList();
            }
        }
        public Dictionary<int, List<TvDB_ImageWideBanner>> GetBySeriesIDs(IEnumerable<int> seriesIDs)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return seriesIDs.ToDictionary(a => a, a => SeriesIDs.GetMultiple(a).ToList());
                return Table.Where(a => seriesIDs.Contains(a.SeriesID)).GroupBy(a => a.SeriesID).ToDictionary(a => a.Key, a => a.ToList());
            }
        }
        public Dictionary<int, List<TvDB_ImageWideBanner>> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));
            Dictionary<int, List<int>> animetvdb = Repo.CrossRef_AniDB_TvDBV2.GetTvsIdByAnimeIDs(animeIds);
            return animetvdb.ToDictionary(a => a.Key, a => GetBySeriesIDs(a.Value).Values.SelectMany(b => b).ToList());
        }
    }
}