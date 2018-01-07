using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_GroupStatusRepository : BaseRepository<AniDB_GroupStatus, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, AniDB_GroupStatus, int> Animes;
        private PocoIndex<int, AniDB_GroupStatus, int, int> AnimeGroups;

        internal override int SelectKey(AniDB_GroupStatus entity) => entity.AniDB_GroupStatusID;
            
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_GroupStatus, int>(Cache, a => a.AnimeID);
            AnimeGroups = new PocoIndex<int, AniDB_GroupStatus, int, int>(Cache, a => a.AnimeID, a => a.GroupID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            AnimeGroups = null;
        }

        internal override void EndDelete(AniDB_GroupStatus entity, object returnFromBeginDelete, object parameters)
        {
            if (entity.AnimeID != 0)
            {
                logger.Trace("Updating group stats by anime from AniDB_GroupStatusRepository.Delete: {0}",entity.AnimeID);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.AnimeID);
            }
        }

        public AniDB_GroupStatus GetByAnimeIDAndGroupID(int animeid, int groupid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeGroups.GetOne(animeid, groupid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.GroupID == groupid);
            }
        }

        public List<AniDB_GroupStatus> GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }

        public void DeleteForAnime(int animeid)
        {
            Delete(GetByAnimeID(animeid));
        }
    }
}