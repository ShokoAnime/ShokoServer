using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_TagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Tag, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Tag, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Tag, int>? _anilistTagIDs;

    protected override int SelectKey(Anilist_Anime_Tag entity)
        => entity.Anilist_Anime_TagID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistTagIDs = Cache.CreateIndex(a => a.AnilistTagID);
    }

    public IReadOnlyList<Anilist_Anime_Tag> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderByDescending(t => t.Weight)
            .ThenBy(t => t.AnilistTagID)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Tag> GetByAnilistTagID(int anilistTagId)
        => _anilistTagIDs!.GetMultiple(anilistTagId)
            .OrderBy(t => t.AnilistAnimeID)
            .ToList();
}
