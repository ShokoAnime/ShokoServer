using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_SimilarRepository : BaseRepository<AniDB_Anime_Similar, int>
    {
        private PocoIndex<int, AniDB_Anime_Similar, int> Animes;
        private PocoIndex<int, AniDB_Anime_Similar, int, int> AnimeSimilars;

        internal override int SelectKey(AniDB_Anime_Similar entity) => entity.AniDB_Anime_SimilarID;
        
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Similar, int>(Cache, a => a.AnimeID);
            AnimeSimilars = new PocoIndex<int, AniDB_Anime_Similar, int, int>(Cache, a => a.AnimeID, a => a.SimilarAnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            AnimeSimilars = null;
        }


        public AniDB_Anime_Similar GetByAnimeIDAndSimilarID(int animeid, int similaranimeid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeSimilars.GetOne(animeid, similaranimeid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.SimilarAnimeID == similaranimeid);
            }
        }

        public List<AniDB_Anime_Similar> GetByAnimeID(int id)
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
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_SimilarID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_SimilarID).ToList();
            }
        }
    }
}