#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_ImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Image, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_Image, string>? _tmdbRemoteFileNames;

    protected override int SelectKey(TMDB_Image entity)
        => entity.TMDB_ImageID;

    public override void PopulateIndexes()
    {
        _tmdbRemoteFileNames = new(Cache, a => a.RemoteFileName);
    }

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieID(int? movieId)
        => GetByForeignID(movieId, ForeignEntityType.Movie);

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieIDAndType(int? movieId, ImageEntityType type)
        => GetByForeignIDAndType(movieId, ForeignEntityType.Movie, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeID(int? episodeId)
        => GetByForeignID(episodeId, ForeignEntityType.Episode);

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeIDAndType(int? episodeId, ImageEntityType type)
        => GetByForeignIDAndType(episodeId, ForeignEntityType.Episode, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonID(int? seasonId)
        => GetByForeignID(seasonId, ForeignEntityType.Season);

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonIDAndType(int? seasonId, ImageEntityType type)
        => GetByForeignIDAndType(seasonId, ForeignEntityType.Season, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbShowID(int? showId)
        => GetByForeignID(showId, ForeignEntityType.Show);

    public IReadOnlyList<TMDB_Image> GetByTmdbShowIDAndType(int? showId, ImageEntityType type)
        => GetByForeignIDAndType(showId, ForeignEntityType.Show, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionID(int? collectionId)
        => GetByForeignID(collectionId, ForeignEntityType.Collection);

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionIDAndType(int? collectionId, ImageEntityType type)
        => GetByForeignIDAndType(collectionId, ForeignEntityType.Collection, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbNetworkID(int? networkId)
        => GetByForeignID(networkId, ForeignEntityType.Network);

    public IReadOnlyList<TMDB_Image> GetByTmdbNetworkIDAndType(int? networkId, ImageEntityType type)
        => GetByForeignIDAndType(networkId, ForeignEntityType.Network, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbCompanyID(int? companyId)
        => GetByForeignID(companyId, ForeignEntityType.Company);

    public IReadOnlyList<TMDB_Image> GetByTmdbCompanyIDAndType(int? companyId, ImageEntityType type)
        => GetByForeignIDAndType(companyId, ForeignEntityType.Company, type);

    public IReadOnlyList<TMDB_Image> GetByTmdbPersonID(int? personId)
        => GetByForeignID(personId, ForeignEntityType.Person);

    public IReadOnlyList<TMDB_Image> GetByTmdbPersonIDAndType(int? personId, ImageEntityType type)
        => GetByForeignIDAndType(personId, ForeignEntityType.Person, type);

    public IReadOnlyList<TMDB_Image> GetByType(ImageEntityType type, bool ofType = true)
        => RepoFactory.TMDB_Image_Entity.GetByImageType(type)
            .Select(x => x.GetTmdbImage(ofType))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<TMDB_Image> GetByForeignID(int? id, ForeignEntityType foreignType, bool ofType = true)
        => RepoFactory.TMDB_Image_Entity.GetByForeignID(id, foreignType)
            .Select(x => x.GetTmdbImage(ofType))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<TMDB_Image> GetByForeignIDAndType(int? id, ForeignEntityType foreignType, ImageEntityType type, bool ofType = true)
        => RepoFactory.TMDB_Image_Entity.GetByForeignIDAndType(id, foreignType, type)
            .Select(x => x.GetTmdbImage(ofType))
            .WhereNotNull()
            .ToList();

    public TMDB_Image? GetByRemoteFileName(string fileName)
        => !string.IsNullOrEmpty(fileName)
            ? ReadLock(() => _tmdbRemoteFileNames!.GetOne(fileName))
            : null;
}
