using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_RecommendationRepository : BaseRepository<AniDB_Recommendation, int>
    {

        private PocoIndex<int, AniDB_Recommendation, int> Animes;
        private PocoIndex<int, AniDB_Recommendation, int, int> AnimeUsers;

        internal override int SelectKey(AniDB_Recommendation entity) => entity.AniDB_RecommendationID;
        
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Recommendation, int>(Cache, a => a.AnimeID);
            AnimeUsers = new PocoIndex<int, AniDB_Recommendation, int, int>(Cache, a => a.AnimeID, a => a.UserID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            AnimeUsers = null;
        }

        public List<AniDB_Recommendation> GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_RecommendationID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_RecommendationID).ToList();
            }
        }
        public AniDB_Recommendation GetByAnimeIDAndUserID(int animeid, int userid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeUsers.GetOne(animeid, userid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.UserID==userid);
            }
        }
    }
}