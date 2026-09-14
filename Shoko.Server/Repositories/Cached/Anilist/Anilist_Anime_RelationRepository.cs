using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_RelationRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Relation, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Relation, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Relation, int>? _relatedAnilistIDs;

    protected override int SelectKey(Anilist_Anime_Relation entity)
        => entity.Anilist_Anime_RelationID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _relatedAnilistIDs = Cache.CreateIndex(a => a.RelatedAnilistID);
    }

    public IReadOnlyList<Anilist_Anime_Relation> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.RelatedAnilistID)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Relation> GetByRelatedAnilistID(int relatedAnilistId)
        => _relatedAnilistIDs!.GetMultiple(relatedAnilistId)
            .OrderBy(x => x.AnilistAnimeID)
            .ToList();
}
