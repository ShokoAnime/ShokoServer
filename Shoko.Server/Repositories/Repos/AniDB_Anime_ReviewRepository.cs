using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_ReviewRepository : BaseRepository<AniDB_Anime_Review, int>
    {
        private PocoIndex<int, AniDB_Anime_Review, int> Animes;
        private PocoIndex<int, AniDB_Anime_Review, int, int> AnimeReviews;

        internal override int SelectKey(AniDB_Anime_Review entity) => entity.AniDB_Anime_ReviewID;
            
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Review, int>(Cache, a => a.AnimeID);
            AnimeReviews = new PocoIndex<int, AniDB_Anime_Review, int, int>(Cache, a => a.AnimeID, a => a.ReviewID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            AnimeReviews = null;
        }

        public AniDB_Anime_Review GetByAnimeIDAndReviewID(int animeid, int reviewid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeReviews.GetOne(animeid, reviewid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.ReviewID == reviewid);
            }
        }

        public List<AniDB_Anime_Review> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_ReviewID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_ReviewID).ToList();
            }
        }
    }
}