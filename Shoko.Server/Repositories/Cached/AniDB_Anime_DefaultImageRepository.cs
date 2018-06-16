using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;

namespace Shoko.Server.Repositories.Cached
{
    public class AniDB_Anime_DefaultImageRepository : BaseCachedRepository<AniDB_Anime_DefaultImage, int>
    {
        private PocoIndex<int, AniDB_Anime_DefaultImage, int> Animes;

        private AniDB_Anime_DefaultImageRepository()
        {
        }

        public static AniDB_Anime_DefaultImageRepository Create()
        {
            var repo = new AniDB_Anime_DefaultImageRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

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
