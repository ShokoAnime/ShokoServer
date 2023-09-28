using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_EpisodeRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Episode, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _anidbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _tmdbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, (int, int)>? _pairedIDs;

    public List<CrossRef_AniDB_TMDB_Episode> GetByAnidbEpisodeID(int episodeId)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId).OrderBy(a => a.Ordering).ToList());

    public List<CrossRef_AniDB_TMDB_Episode> GetByTmdbEpisodeID(int episodeId)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId).OrderBy(a => a.Ordering).ToList());

    public CrossRef_AniDB_TMDB_Episode? GetByAnidbEpisodeAndTmdbEpisodeIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetOne((anidbId, tmdbId)));

    protected override int SelectKey(CrossRef_AniDB_TMDB_Episode entity)
        => entity.CrossRef_AniDB_TMDB_EpisodeID;

    public override void PopulateIndexes()
    {
        _tmdbEpisodeIDs = new(Cache, a => a.TmdbEpisodeID);
        _anidbEpisodeIDs = new(Cache, a => a.AnidbEpisodeID);
        _pairedIDs = new(Cache, a => (a.AnidbEpisodeID, a.TmdbEpisodeID));
    }

    public override void RegenerateDb()
    {
    }
}
