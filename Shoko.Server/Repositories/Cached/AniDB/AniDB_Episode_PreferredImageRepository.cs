#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Episode_PreferredImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Episode_PreferredImage, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Episode_PreferredImage, int>? _episodeIDs;

    private PocoIndex<int, AniDB_Episode_PreferredImage, (DataSourceType, ImageEntityType, int)>? _imageTypes;

    protected override int SelectKey(AniDB_Episode_PreferredImage entity)
        => entity.AniDB_Episode_PreferredImageID;

    public override void PopulateIndexes()
    {
        _episodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeID);
        _imageTypes = Cache.CreateIndex(a => (a.ImageSource, a.ImageType, a.ImageID));
    }

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndType(int episodeID, ImageEntityType imageType)
        => GetByEpisodeID(episodeID).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndTypeAndSource(int episodeID, ImageEntityType imageType, DataSourceType imageSource)
        => GetByEpisodeID(episodeID).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public IReadOnlyList<AniDB_Episode_PreferredImage> GetByEpisodeID(int episodeId)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeId));

    public IReadOnlyList<AniDB_Episode_PreferredImage> GetByImageSourceAndTypeAndID(DataSourceType imageSource, ImageEntityType imageType, int imageID)
        => ReadLock(() => _imageTypes!.GetMultiple((imageSource, imageType, imageID)));
}
