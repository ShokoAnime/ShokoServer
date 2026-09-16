using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_StudioRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Studio, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Studio, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Studio, int>? _anilistStudioIDs;

    protected override int SelectKey(Anilist_Anime_Studio entity)
        => entity.Anilist_Anime_StudioID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistStudioIDs = Cache.CreateIndex(a => a.AnilistStudioID);
    }

    public IReadOnlyList<Anilist_Anime_Studio> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderByDescending(x => x.IsMainStudio)
            .ThenBy(x => x.AnilistStudioID)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Studio> GetByAnilistStudioID(int anilistStudioId)
        => _anilistStudioIDs!.GetMultiple(anilistStudioId)
            .OrderBy(x => x.AnilistAnimeID)
            .ToList();
}
