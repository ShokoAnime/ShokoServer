using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Episode, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Episode, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Episode, int>? _anilistEpisodeIDs;

    private PocoIndex<int, Anilist_Episode, int?>? _anilistScheduleEpisodeIDs;

    protected override int SelectKey(Anilist_Episode entity)
        => entity.Anilist_EpisodeID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistEpisodeIDs = Cache.CreateIndex(a => a.AnilistEpisodeID);
        _anilistScheduleEpisodeIDs = Cache.CreateIndex(a => a.AnilistScheduleEpisodeID);
    }

    public IReadOnlyList<Anilist_Episode> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(e => e.EpisodeNumber)
            .ToList();

    public Anilist_Episode? GetByAnilistEpisodeID(int anilistEpisodeId)
        => _anilistEpisodeIDs!.GetOne(anilistEpisodeId);

    public Anilist_Episode? GetByAnilistScheduleEpisodeID(int anilistScheduleEpisodeId)
        => _anilistScheduleEpisodeIDs!.GetOne(anilistScheduleEpisodeId);
}
