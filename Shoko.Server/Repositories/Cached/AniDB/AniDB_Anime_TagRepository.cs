#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_TagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Tag, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Tag, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Tag, int>? _tagIDs;

    protected override int SelectKey(AniDB_Anime_Tag entity)
        => entity.AniDB_Anime_TagID;

    public override void PopulateIndexes()
    {
        _animeIDs = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.AnimeID);
        _tagIDs = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.TagID);
    }

    public AniDB_Anime_Tag? GetByAnimeIDAndTagID(int animeID, int tagID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID).FirstOrDefault(a => a.TagID == tagID));

    public List<AniDB_Anime_Tag> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public List<AniDB_Anime_Tag> GetByTagID(int tagID)
        => ReadLock(() => _tagIDs!.GetMultiple(tagID));

    /// <summary>
    /// Gets all the anime tags, but only if we have the anime locally
    /// </summary>
    /// <returns></returns>
    public List<AniDB_Anime_Tag> GetAllForLocalSeries()
        => RepoFactory.AnimeSeries.GetAll()
            .SelectMany(a => GetByAnimeID(a.AniDB_ID))
            .WhereNotNull()
            .Distinct()
            .ToList();
}
