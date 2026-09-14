using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_ExternalLinkRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_ExternalLink, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_ExternalLink, int>? _anilistAnimeIDs;

    protected override int SelectKey(Anilist_Anime_ExternalLink entity)
        => entity.Anilist_Anime_ExternalLinkID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
    }

    public IReadOnlyList<Anilist_Anime_ExternalLink> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.LinkType)
            .ThenBy(x => x.Site)
            .ThenBy(x => x.AnilistLinkID)
            .ToList();
}
