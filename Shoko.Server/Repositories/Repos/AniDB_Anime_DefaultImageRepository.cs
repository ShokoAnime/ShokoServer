using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_DefaultImageRepository : BaseRepository<AniDB_Anime_DefaultImage, int>
    {
        private PocoIndex<int, AniDB_Anime_DefaultImage, int> Animes;

        internal override int SelectKey(AniDB_Anime_DefaultImage entity) => entity.AniDB_Anime_DefaultImageID;
        
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_DefaultImage, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
        }

        public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, int imageType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).FirstOrDefault(a=>a.ImageType==imageType);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.ImageType == imageType);
            }
        }


        public List<AniDB_Anime_DefaultImage> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public Dictionary<int, List<AniDB_Anime_DefaultImage>> GetByAnimeIDs(IEnumerable<int> ids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ids.ToDictionary(a => a, a => Animes.GetMultiple(a));
                return Table.Where(a => ids.Contains(a.AnimeID)).GroupBy(a=>a.AnimeID).ToDictionary(a => a.Key, a => a.ToList());
            }
        }
    }
}