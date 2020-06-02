using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class AniDB_Anime_DefaultImageRepository : BaseCachedRepository<AniDB_Anime_DefaultImage, int>
    {
        private PocoIndex<int, AniDB_Anime_DefaultImage, int> Animes;

        public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, int imageType)
        {
            return Animes.GetMultiple(animeid).FirstOrDefault(a => a.ImageType == imageType);
        }

        public List<AniDB_Anime_DefaultImage> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
        }

        protected override int SelectKey(AniDB_Anime_DefaultImage entity)
        {
            return entity.AniDB_Anime_DefaultImageID;
        }

        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_DefaultImage, int>(Cache, a => a.AnimeID);
        }

        public override void RegenerateDb()
        {
        }
    }
}
