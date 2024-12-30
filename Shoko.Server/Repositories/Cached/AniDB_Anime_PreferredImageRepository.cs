using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_PreferredImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_PreferredImage, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_PreferredImage, int>? _animeIDs;

    protected override int SelectKey(AniDB_Anime_PreferredImage entity)
        => entity.AniDB_Anime_PreferredImageID;

    public override void PopulateIndexes()
    {
        _animeIDs = new(Cache, a => a.AnidbAnimeID);
    }

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndType(int animeID, ImageEntityType imageType)
        => GetByAnimeID(animeID).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndTypeAndSource(int animeID, ImageEntityType imageType, DataSourceType imageSource)
        => GetByAnimeID(animeID).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public List<AniDB_Anime_PreferredImage> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));
}
