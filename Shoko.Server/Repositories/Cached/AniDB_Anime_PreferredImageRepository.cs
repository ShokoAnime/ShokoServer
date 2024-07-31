using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_PreferredImageRepository : BaseCachedRepository<AniDB_Anime_PreferredImage, int>
{
    private PocoIndex<int, AniDB_Anime_PreferredImage, int>? AnimeIDs;

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndType(int animeId, ImageEntityType imageType)
        => GetByAnimeID(animeId).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Anime_PreferredImage? GetByAnidbAnimeIDAndTypeAndSource(int animeId, ImageEntityType imageType, DataSourceType imageSource)
        => GetByAnimeID(animeId).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public List<AniDB_Anime_PreferredImage> GetByAnimeID(int animeId)
        => ReadLock(() => AnimeIDs!.GetMultiple(animeId));

    protected override int SelectKey(AniDB_Anime_PreferredImage entity)
        => entity.AniDB_Anime_PreferredImageID;

    public override void PopulateIndexes()
    {
        AnimeIDs = new(Cache, a => a.AnidbAnimeID);
    }

    public override void RegenerateDb()
    {
    }

    public AniDB_Anime_PreferredImageRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    { }
}
