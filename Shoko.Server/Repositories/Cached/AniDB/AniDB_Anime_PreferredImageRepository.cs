#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_PreferredImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_PreferredImage, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_PreferredImage, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_PreferredImage, (DataSourceType, ImageEntityType, int)>? _imageTypes;

    protected override int SelectKey(AniDB_Anime_PreferredImage entity)
        => entity.AniDB_Anime_PreferredImageID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnidbAnimeID);
        _imageTypes = Cache.CreateIndex(a => (a.ImageSource, a.ImageType, a.ImageID));
    }

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndType(int animeID, ImageEntityType imageType)
        => GetByAnimeID(animeID).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndTypeAndSource(int animeID, ImageEntityType imageType, DataSourceType imageSource)
        => GetByAnimeID(animeID).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public IReadOnlyList<AniDB_Anime_PreferredImage> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public IReadOnlyList<AniDB_Anime_PreferredImage> GetByImageSourceAndTypeAndID(DataSourceType imageSource, ImageEntityType imageType, int imageID)
        => ReadLock(() => _imageTypes!.GetMultiple((imageSource, imageType, imageID)));
}
