#nullable enable
using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_Image_EntityRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Image_Entity, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_Image_Entity, ImageEntityType>? _byImageType;

    private PocoIndex<int, TMDB_Image_Entity, ForeignEntityType>? _byEntityType;

    private PocoIndex<int, TMDB_Image_Entity, (ForeignEntityType, int)>? _byEntityTypeAndEntityID;

    private PocoIndex<int, TMDB_Image_Entity, (ForeignEntityType, ImageEntityType, int)>? _byEntityTypeAndImageTypeAndEntityID;

    private PocoIndex<int, TMDB_Image_Entity, (ForeignEntityType, ImageEntityType, int, string)>? _byEntityTypeAndImageTypeAndEntityIDAndRemoteFileName;

    private PocoIndex<int, TMDB_Image_Entity, string>? _tmdbRemoteFileNames;

    protected override int SelectKey(TMDB_Image_Entity entity)
        => entity.TMDB_Image_EntityID;

    public override void PopulateIndexes()
    {
        _byImageType = new(Cache, a => a.ImageType);
        _byEntityType = new(Cache, a => a.TmdbEntityType);
        _byEntityTypeAndEntityID = new(Cache, a => (a.TmdbEntityType, a.TmdbEntityID));
        _byEntityTypeAndImageTypeAndEntityID = new(Cache, a => (a.TmdbEntityType, a.ImageType, a.TmdbEntityID));
        _byEntityTypeAndImageTypeAndEntityIDAndRemoteFileName = new(Cache, a => (a.TmdbEntityType, a.ImageType, a.TmdbEntityID, a.RemoteFileName));
        _tmdbRemoteFileNames = new(Cache, a => a.RemoteFileName);
    }

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbMovieID(int? movieId)
        => GetByForeignID(movieId, ForeignEntityType.Movie);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbMovieIDAndType(int? movieId, ImageEntityType type)
        => GetByForeignIDAndType(movieId, ForeignEntityType.Movie, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbEpisodeID(int? episodeId)
        => GetByForeignID(episodeId, ForeignEntityType.Episode);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbEpisodeIDAndType(int? episodeId, ImageEntityType type)
        => GetByForeignIDAndType(episodeId, ForeignEntityType.Episode, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbSeasonID(int? seasonId)
        => GetByForeignID(seasonId, ForeignEntityType.Season);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbSeasonIDAndType(int? seasonId, ImageEntityType type)
        => GetByForeignIDAndType(seasonId, ForeignEntityType.Season, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbShowID(int? showId)
        => GetByForeignID(showId, ForeignEntityType.Show);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbShowIDAndType(int? showId, ImageEntityType type)
        => GetByForeignIDAndType(showId, ForeignEntityType.Show, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbCollectionID(int? collectionId)
        => GetByForeignID(collectionId, ForeignEntityType.Collection);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbCollectionIDAndType(int? collectionId, ImageEntityType type)
        => GetByForeignIDAndType(collectionId, ForeignEntityType.Collection, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbNetworkID(int? networkId)
        => GetByForeignID(networkId, ForeignEntityType.Network);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbNetworkIDAndType(int? networkId, ImageEntityType type)
        => GetByForeignIDAndType(networkId, ForeignEntityType.Network, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbCompanyID(int? companyId)
        => GetByForeignID(companyId, ForeignEntityType.Company);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbCompanyIDAndType(int? companyId, ImageEntityType type)
        => GetByForeignIDAndType(companyId, ForeignEntityType.Company, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbPersonID(int? personId)
        => GetByForeignID(personId, ForeignEntityType.Person);

    public IReadOnlyList<TMDB_Image_Entity> GetByTmdbPersonIDAndType(int? personId, ImageEntityType type)
        => GetByForeignIDAndType(personId, ForeignEntityType.Person, type);

    public IReadOnlyList<TMDB_Image_Entity> GetByImageType(ImageEntityType type)
        => ReadLock(() => _byImageType!.GetMultiple(type)) ?? [];

    public IReadOnlyList<TMDB_Image_Entity> GetByForeignType(ForeignEntityType entityType)
        => ReadLock(() => _byEntityType!.GetMultiple(entityType)) ?? [];

    public IReadOnlyList<TMDB_Image_Entity> GetByForeignID(int? entityId, ForeignEntityType entityType)
        => entityId.HasValue ? ReadLock(() => _byEntityTypeAndEntityID!.GetMultiple((entityType, entityId.Value))) ?? [] : [];

    public IReadOnlyList<TMDB_Image_Entity> GetByForeignIDAndType(int? entityId, ForeignEntityType entityType, ImageEntityType imageType)
        => entityId.HasValue ? ReadLock(() => _byEntityTypeAndImageTypeAndEntityID!.GetMultiple((entityType, imageType, entityId.Value))) ?? [] : [];

    public TMDB_Image_Entity? GetByForeignIDAndTypeAndRemoteFileName(int? entityId, ForeignEntityType entityType, ImageEntityType imageType, string remoteFileName)
        => entityId.HasValue ? ReadLock(() => _byEntityTypeAndImageTypeAndEntityIDAndRemoteFileName!.GetOne((entityType, imageType, entityId.Value, remoteFileName))) : null;

    public IReadOnlyList<TMDB_Image_Entity> GetByRemoteFileName(string fileName)
        => !string.IsNullOrEmpty(fileName)
            ? ReadLock(() => _tmdbRemoteFileNames!.GetMultiple(fileName))
            : [];
}
