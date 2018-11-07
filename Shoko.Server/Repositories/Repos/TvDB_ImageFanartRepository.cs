using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class TvDB_ImageFanartRepository : BaseRepository<TvDB_ImageFanart, int>
    {
        private PocoIndex<int, TvDB_ImageFanart, int> SeriesIDs;
        private PocoIndex<int, TvDB_ImageFanart, int> TvDBIDs;

        internal override int SelectKey(TvDB_ImageFanart entity) => entity.TvDB_ImageFanartID;

        internal override void PopulateIndexes()
        {
            SeriesIDs = new PocoIndex<int, TvDB_ImageFanart, int>(Cache, a => a.SeriesID);
            TvDBIDs = new PocoIndex<int, TvDB_ImageFanart, int>(Cache, a => a.Id);
        }

        internal override void ClearIndexes()
        {
            SeriesIDs = null;
            TvDBIDs = null;
        }


        public TvDB_ImageFanart GetByTvDBID(int id)
        {

            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.Id==id);
            }
        }

        public List<TvDB_ImageFanart> GetBySeriesID(int seriesID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID);
                return Table.Where(a => a.SeriesID == seriesID).ToList();
            }
        }
        public Dictionary<int, List<TvDB_ImageFanart>> GetBySeriesIDs(IEnumerable<int> seriesIDs)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return seriesIDs.ToDictionary(a=>a,a=>SeriesIDs.GetMultiple(a).ToList());
                return Table.Where(a => seriesIDs.Contains(a.SeriesID)).GroupBy(a => a.SeriesID).ToDictionary(a => a.Key, a => a.ToList());
            }
        }
        public Dictionary<int, List<TvDB_ImageFanart>> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));
            Dictionary<int, List<int>> animetvdb = Repo.Instance.CrossRef_AniDB_TvDBV2.GetTvsIdByAnimeIDs(animeIds);
            return animetvdb.ToDictionary(a => a.Key, a => GetBySeriesIDs(a.Value).Values.SelectMany(b=>b).ToList());
        }



    }
}