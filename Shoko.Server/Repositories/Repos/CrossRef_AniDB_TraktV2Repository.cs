using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TraktV2Repository : BaseRepository<CrossRef_AniDB_TraktV2, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TraktV2, int> Animes;
        private PocoIndex<int, CrossRef_AniDB_TraktV2, string> Trakts;

        internal override int SelectKey(CrossRef_AniDB_TraktV2 entity) => entity.CrossRef_AniDB_TraktV2ID;
            

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, CrossRef_AniDB_TraktV2, int>(Cache, a => a.AnimeID);
            Trakts = new PocoIndex<int, CrossRef_AniDB_TraktV2, string>(Cache, a => a.TraktID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            Trakts = null;
        }

        public List<CrossRef_AniDB_TraktV2> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).OrderBy(a=>a.AniDBStartEpisodeType).ThenBy(a=>a.AniDBStartEpisodeNumber).ToList();
                return Table.Where(a => a.AnimeID == id).OrderBy(a => a.AniDBStartEpisodeType).ThenBy(a => a.AniDBStartEpisodeNumber).ToList();
            }
        }


        public List<CrossRef_AniDB_TraktV2> GetByAnimeIDEpTypeEpNumber(int id, int aniEpType,
            int aniEpisodeNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Where(a => a.AniDBStartEpisodeType==aniEpType && a.AniDBStartEpisodeNumber==aniEpisodeNumber).ToList();
                return Table.Where(a => a.AnimeID == id && a.AniDBStartEpisodeType == aniEpType && a.AniDBStartEpisodeNumber == aniEpisodeNumber).ToList();
            }
        }

        public CrossRef_AniDB_TraktV2 GetByTraktID(string id, int season, int episodeNumber, int animeID, int aniEpType, int aniEpisodeNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Trakts.GetMultiple(id).FirstOrDefault(a => a.AniDBStartEpisodeType == aniEpType && a.AniDBStartEpisodeNumber == aniEpisodeNumber && a.AnimeID==animeID && a.TraktSeasonNumber==season && a.TraktStartEpisodeNumber==episodeNumber);
                return Table.FirstOrDefault(a => a.TraktID==id && a.AniDBStartEpisodeType == aniEpType && a.AniDBStartEpisodeNumber == aniEpisodeNumber && a.AnimeID == animeID && a.TraktSeasonNumber == season && a.TraktStartEpisodeNumber == episodeNumber);
            }
        }



        public List<CrossRef_AniDB_TraktV2> GetByTraktIDAndSeason(string traktID, int season)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Trakts.GetMultiple(traktID).Where(a => a.TraktSeasonNumber==season).ToList();
                return Table.Where(a => a.TraktID==traktID && a.TraktSeasonNumber == season).ToList();
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetByTraktID(string traktID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Trakts.GetMultiple(traktID);
                return Table.Where(a => a.TraktID == traktID).ToList();
            }
        }
    }
}