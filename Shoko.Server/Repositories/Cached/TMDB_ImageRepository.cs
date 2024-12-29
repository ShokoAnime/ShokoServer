using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class TMDB_ImageRepository : BaseCachedRepository<TMDB_Image, int>
{
    private PocoIndex<int, TMDB_Image, int?>? _tmdbMovieIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbEpisodeIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbSeasonIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbShowIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbCollectionIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbNetworkIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbCompanyIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbPersonIDs;
    private PocoIndex<int, TMDB_Image, ImageEntityType>? _tmdbTypes;
    private PocoIndex<int, TMDB_Image, (string filePath, ImageEntityType type)>? _tmdbRemoteFileNames;

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieID(int? movieId)
        => movieId.HasValue ? ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieIDAndType(int? movieId, ImageEntityType type)
        => GetByTmdbMovieID(movieId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeID(int? episodeId)
        => episodeId.HasValue ? ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeIDAndType(int? episodeId, ImageEntityType type)
        => GetByTmdbEpisodeID(episodeId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonID(int? seasonId)
        => seasonId.HasValue ? ReadLock(() => _tmdbSeasonIDs!.GetMultiple(seasonId)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonIDAndType(int? seasonId, ImageEntityType type)
        => GetByTmdbSeasonID(seasonId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbShowID(int? showId)
        => showId.HasValue ? ReadLock(() => _tmdbShowIDs!.GetMultiple(showId)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbShowIDAndType(int? showId, ImageEntityType type)
        => GetByTmdbShowID(showId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionID(int? collectionId)
        => collectionId.HasValue ? ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionIDAndType(int? collectionId, ImageEntityType type)
        => ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbNetworkID(int? networkId)
        => networkId.HasValue ? ReadLock(() => _tmdbNetworkIDs!.GetMultiple(networkId.Value)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbNetworkIDAndType(int? networkId, ImageEntityType type)
        => GetByTmdbNetworkID(networkId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbCompanyID(int? companyId)
        => companyId.HasValue ? ReadLock(() => _tmdbCompanyIDs!.GetMultiple(companyId.Value)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbCompanyIDAndType(int? companyId, ImageEntityType type)
        => GetByTmdbCompanyID(companyId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbPersonID(int? personId)
        => personId.HasValue ? ReadLock(() => _tmdbPersonIDs!.GetMultiple(personId.Value)) ?? [] : [];

    public IReadOnlyList<TMDB_Image> GetByTmdbPersonIDAndType(int? personId, ImageEntityType type)
        => GetByTmdbPersonID(personId).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByType(ImageEntityType type)
        => ReadLock(() => _tmdbTypes!.GetMultiple(type)) ?? [];

    public IReadOnlyList<TMDB_Image> GetByForeignID(int? id, ForeignEntityType foreignType)
        => foreignType switch
        {
            ForeignEntityType.Movie => GetByTmdbMovieID(id),
            ForeignEntityType.Episode => GetByTmdbEpisodeID(id),
            ForeignEntityType.Season => GetByTmdbSeasonID(id),
            ForeignEntityType.Show => GetByTmdbShowID(id),
            ForeignEntityType.Collection => GetByTmdbCollectionID(id),
            _ => new List<TMDB_Image>(),
        };

    public IReadOnlyList<TMDB_Image> GetByForeignIDAndType(int? id, ForeignEntityType foreignType, ImageEntityType type)
        => foreignType switch
        {
            ForeignEntityType.Movie => GetByTmdbMovieIDAndType(id, type),
            ForeignEntityType.Episode => GetByTmdbEpisodeIDAndType(id, type),
            ForeignEntityType.Season => GetByTmdbSeasonIDAndType(id, type),
            ForeignEntityType.Show => GetByTmdbShowIDAndType(id, type),
            ForeignEntityType.Collection => GetByTmdbCollectionIDAndType(id, type),
            _ => new List<TMDB_Image>(),
        };

    public TMDB_Image? GetByRemoteFileNameAndType(string fileName, ImageEntityType type)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        if (fileName.EndsWith(".svg"))
            fileName = fileName[..^4] + ".png";
        return ReadLock(() => _tmdbRemoteFileNames!.GetOne((fileName, type)));
    }

    public ILookup<int, TMDB_Image> GetByAnimeIDsAndType(int[] animeIds, ImageEntityType type)
    {
        return animeIds
            .SelectMany(animeId =>
                RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbMovieIDAndType(xref.TmdbMovieID, type))
                .Concat(RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbShowIDAndType(xref.TmdbShowID, type)))
                .Select(image => (AnimeID: animeId, Image: image))
            )
            .ToLookup(a => a.AnimeID, a => a.Image);
    }

    protected override int SelectKey(TMDB_Image entity)
        => entity.TMDB_ImageID;

    public override void PopulateIndexes()
    {
        _tmdbMovieIDs = new(Cache, a => a.TmdbMovieID);
        _tmdbEpisodeIDs = new(Cache, a => a.TmdbEpisodeID);
        _tmdbSeasonIDs = new(Cache, a => a.TmdbSeasonID);
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _tmdbCollectionIDs = new(Cache, a => a.TmdbCollectionID);
        _tmdbNetworkIDs = new(Cache, a => a.TmdbNetworkID);
        _tmdbCompanyIDs = new(Cache, a => a.TmdbCompanyID);
        _tmdbPersonIDs = new(Cache, a => a.TmdbPersonID);
        _tmdbTypes = new(Cache, a => a.ImageType);
        _tmdbRemoteFileNames = new(Cache, a => (a.RemoteFileName, a.ImageType));
    }

    public override void RegenerateDb()
    {
    }

    public TMDB_ImageRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
