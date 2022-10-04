using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_DefaultImageRepository : BaseCachedRepository<AniDB_Anime_DefaultImage, int>
{
    private PocoIndex<int, AniDB_Anime_DefaultImage, int> Animes;

    public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, ImageSizeType imageType)
    {
        return GetByAnimeID(animeid).FirstOrDefault(a => a.ImageType == (int)imageType);
    }

    public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeTypeAndImageEntityType(int animeid,
        ImageSizeType imageType, ImageEntityType entityType)
    {
        var defaultImage = GetByAnimeIDAndImagezSizeType(animeid, imageType);
        return defaultImage != null && defaultImage.ImageParentType == (int)entityType ? defaultImage : null;
    }

    public AniDB_Anime_DefaultImage GetByAnimeIDAndImageEntityType(int animeid, ImageEntityType entityType)
    {
        return GetByAnimeID(animeid).FirstOrDefault(a => a.ImageParentType == (int)entityType);
    }

    public List<AniDB_Anime_DefaultImage> GetByAnimeID(int id)
    {
        return ReadLock(() => Animes.GetMultiple(id));
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
