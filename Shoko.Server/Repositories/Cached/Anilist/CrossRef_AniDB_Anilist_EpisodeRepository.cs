using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class CrossRef_AniDB_Anilist_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_Anilist_Episode, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, int>? _anidbEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, int>? _anilistAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, int>? _anilistEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, (int, int)>? _pairedAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Episode, (int, int)>? _pairedEpisodeIDs;

    protected override int SelectKey(CrossRef_AniDB_Anilist_Episode entity)
        => entity.CrossRef_AniDB_Anilist_EpisodeID;

    public override void PopulateIndexes()
    {
        _anidbAnimeIDs = Cache.CreateIndex(a => a.AnidbAnimeID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeID);
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistEpisodeIDs = Cache.CreateIndex(a => a.AnilistEpisodeID);
        _pairedAnimeIDs = Cache.CreateIndex(a => (a.AnidbAnimeID, a.AnilistAnimeID));
        _pairedEpisodeIDs = Cache.CreateIndex(a => (a.AnidbEpisodeID, a.AnilistEpisodeID));
    }

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetByAnidbAnimeID(int animeId)
        => _anidbAnimeIDs!.GetMultiple(animeId)
            .OrderBy(e => e.AnidbEpisodeID)
            .ThenBy(e => e.Ordering)
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetByAnidbEpisodeID(int episodeId)
        => _anidbEpisodeIDs!.GetMultiple(episodeId)
            .OrderBy(e => e.Ordering)
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(e => e.EpisodeNumber)
            .ThenBy(e => e.Ordering)
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetByAnilistEpisodeID(int anilistEpisodeId)
        => _anilistEpisodeIDs!.GetMultiple(anilistEpisodeId)
            .OrderBy(e => e.Ordering)
            .ToList();

    public CrossRef_AniDB_Anilist_Episode? GetByAnidbEpisodeAndAnilistEpisodeIDs(int anidbEpisodeId, int anilistEpisodeId)
        => _pairedEpisodeIDs!.GetOne((anidbEpisodeId, anilistEpisodeId));

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetAllByAnidbAnimeAndAnilistAnimeIDs(int anidbId, int anilistId)
        => GetByAnilistAnimeID(anilistId).Concat(GetByAnidbAnimeID(anidbId)).ToList();

    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> GetOnlyByAnidbAnimeAndAnilistAnimeIDs(int anidbId, int anilistId)
        => _pairedAnimeIDs!.GetMultiple((anidbId, anilistId))
            .OrderBy(e => e.AnidbEpisodeID)
            .ThenBy(e => e.Ordering)
            .ToList();
}
