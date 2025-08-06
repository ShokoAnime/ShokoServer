#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_TagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Tag, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Tag, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Tag, int>? _tagIDs;

    protected override int SelectKey(AniDB_Anime_Tag entity)
        => entity.AniDB_Anime_TagID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _tagIDs = Cache.CreateIndex(a => a.TagID);
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
            .Where(a => a != null)
            .Distinct()
            .ToList();
}
