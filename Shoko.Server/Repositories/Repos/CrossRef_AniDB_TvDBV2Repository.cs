using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TvDBV2Repository : BaseRepository<CrossRef_AniDB_TvDBV2, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDBV2, int> TvDBIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDBV2, int> AnimeIDs;

        internal override int SelectKey(CrossRef_AniDB_TvDBV2 entity) => entity.CrossRef_AniDB_TvDBV2ID;


        internal override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, CrossRef_AniDB_TvDBV2, int>(Cache, a => a.TvDBID);
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDBV2, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            TvDBIDs = null;
            AnimeIDs = null;
        }


        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id).OrderBy(xref => xref.AniDBStartEpisodeType)
                        .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
                return Table.Where(a => a.AnimeID == id).OrderBy(xref => xref.AniDBStartEpisodeType)
                    .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetByTvDBID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetMultiple(id).OrderBy(xref => xref.AniDBStartEpisodeType)
                        .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
                return Table.Where(a => a.TvDBID == id).OrderBy(xref => xref.AniDBStartEpisodeType)
                    .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
            }
        }

        public Dictionary<int, List<int>> GetTvsIdByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                return new Dictionary<int, List<int>>();

            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return animeIds.ToDictionary(a=>a,a=>AnimeIDs.GetMultiple(a).Select(b=>b.TvDBID).ToList());
                return Table.Where(a => animeIds.Contains(a.AnimeID)).GroupBy(a => a.AnimeID).ToDictionary(a => a.Key, a => a.Select(b => b.TvDBID).ToList());
            }
        }
        public Dictionary<int, List<CrossRef_AniDB_TvDBV2>> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                return new Dictionary<int, List<CrossRef_AniDB_TvDBV2>>();

            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return animeIds.ToDictionary(a=>a,a=>AnimeIDs.GetMultiple(a).OrderBy(xref => xref.AniDBStartEpisodeType).ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList());
                return Table.Where(a => animeIds.Contains(a.AnimeID)).OrderBy(xref => xref.AniDBStartEpisodeType).ThenBy(xref => xref.AniDBStartEpisodeNumber).GroupBy(a=>a.AnimeID).ToDictionary(a=>a.Key,a=>a.ToList());
            }
        }
        public List<CrossRef_AniDB_TvDBV2> GetByAnimeIDEpTypeEpNumber(int id, int aniEpType, int aniEpisodeNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id)
                        .Where(xref => xref.AniDBStartEpisodeType == aniEpType &&
                                       xref.AniDBStartEpisodeNumber <= aniEpisodeNumber)
                        .OrderByDescending(xref => xref.AniDBStartEpisodeNumber).ToList();
                return Table.Where(a => a.AnimeID==id && a.AniDBStartEpisodeType == aniEpType &&
                                        a.AniDBStartEpisodeNumber <= aniEpisodeNumber).ToList();
            }
        }

        public CrossRef_AniDB_TvDBV2 GetByTvDBID(int id, int season, int episodeNumber, int animeID,
            int aniEpType, int aniEpisodeNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetMultiple(id).FirstOrDefault(xref => xref.TvDBSeasonNumber == season &&
                                                                          xref.TvDBStartEpisodeNumber ==
                                                                          episodeNumber &&
                                                                          xref.AnimeID == animeID &&
                                                                          xref.AniDBStartEpisodeType == aniEpType &&
                                                                          xref.AniDBStartEpisodeNumber ==
                                                                          aniEpisodeNumber);
                return Table.Where(a=>a.TvDBID==id).FirstOrDefault(xref => xref.TvDBSeasonNumber == season &&
                                                                           xref.TvDBStartEpisodeNumber ==
                                                                           episodeNumber &&
                                                                           xref.AnimeID == animeID &&
                                                                           xref.AniDBStartEpisodeType == aniEpType &&
                                                                           xref.AniDBStartEpisodeNumber ==
                                                                           aniEpisodeNumber);
            }
        }
    }
}