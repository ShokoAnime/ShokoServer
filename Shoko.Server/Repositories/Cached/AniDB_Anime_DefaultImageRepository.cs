using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;

namespace Shoko.Server.Repositories.Cached
{
    public class AniDB_Anime_DefaultImageRepository : BaseRepository<AniDB_Anime_DefaultImage, int>
    {
        private PocoIndex<int, AniDB_Anime_DefaultImage, int> Animes;
        public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, int imageType)
        {
            return Animes.GetMultiple(animeid).FirstOrDefault(a => a.ImageType == imageType);
        }

        public List<AniDB_Anime_DefaultImage> GetByAnimeID(int id)
        {
            //Todo: FIX
            return Animes.GetMultiple(id);
        }

        internal override int SelectKey(AniDB_Anime_DefaultImage entity)
        {
            return entity.AniDB_Anime_DefaultImageID;
        }

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_DefaultImage, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
        }
    }
}
