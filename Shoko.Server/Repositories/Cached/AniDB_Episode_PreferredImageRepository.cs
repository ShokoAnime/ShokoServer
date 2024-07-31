using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_Episode_PreferredImageRepository : BaseCachedRepository<AniDB_Episode_PreferredImage, int>
{
    private PocoIndex<int, AniDB_Episode_PreferredImage, int>? _episodeIDs;

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndType(int episodeId, ImageEntityType imageType)
        => GetByEpisodeID(episodeId).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndTypeAndSource(int episodeId, ImageEntityType imageType, DataSourceType imageSource)
        => GetByEpisodeID(episodeId).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public List<AniDB_Episode_PreferredImage> GetByEpisodeID(int episodeId)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeId));

    protected override int SelectKey(AniDB_Episode_PreferredImage entity)
        => entity.AniDB_Episode_PreferredImageID;

    public override void PopulateIndexes()
    {
        _episodeIDs = new(Cache, a => a.AnidbEpisodeID);
    }

    public override void RegenerateDb()
    {
    }

    public AniDB_Episode_PreferredImageRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    { }
}
