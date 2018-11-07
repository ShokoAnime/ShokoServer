using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_MALRepository : BaseRepository<CrossRef_AniDB_MAL, int>
    {

        private PocoIndex<int, CrossRef_AniDB_MAL, int> Animes;
        private PocoIndex<int, CrossRef_AniDB_MAL, int> Mals;

        internal override int SelectKey(CrossRef_AniDB_MAL entity) => entity.CrossRef_AniDB_MALID;
        
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.AnimeID);
            Mals = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.MALID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            Mals = null;
        }
        public List<CrossRef_AniDB_MAL> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }

        public ILookup<int, CrossRef_AniDB_MAL> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (!animeIds.Any())
                return EmptyLookup<int, CrossRef_AniDB_MAL>.Instance;
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return GetAll().Where(a => animeIds.Contains(a.AnimeID)).OrderBy(a => a.StartEpisodeType).ThenBy(a => a.StartEpisodeNumber).ToLookup(s => s.AnimeID);

                return Table.Where(a => animeIds.Contains(a.AnimeID)).OrderBy(a => a.StartEpisodeType).ThenBy(a => a.StartEpisodeNumber).ToLookup(s => s.AnimeID);
            }
        }

        public List<CrossRef_AniDB_MAL> GetByMALID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Mals.GetMultiple(id);
                return Table.Where(a => a.MALID == id).ToList();
            }
        }

        public CrossRef_AniDB_MAL GetByAnimeConstraint(int animeID, int epType, int epNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID).FirstOrDefault(a=> a.StartEpisodeType==epType && a.StartEpisodeNumber==epNumber);
                return Table.FirstOrDefault(a => a.AnimeID == animeID && a.StartEpisodeType == epType && a.StartEpisodeNumber == epNumber);
            }
        }
    }
}