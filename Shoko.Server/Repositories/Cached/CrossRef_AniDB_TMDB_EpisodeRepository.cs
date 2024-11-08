using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_EpisodeRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Episode, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _anidbAnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _anidbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _tmdbShowIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _tmdbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, (int, int)>? _pairedIDs;

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId).ToList());

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByAnidbEpisodeID(int episodeId)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId).OrderBy(a => a.Ordering).ToList());

    public CrossRef_AniDB_TMDB_Episode? GetByAnidbEpisodeAndTmdbEpisodeIDs(int anidbEpisodeId, int tmdbEpisodeId)
        => GetByAnidbAnimeID(anidbEpisodeId).FirstOrDefault(xref => xref.TmdbEpisodeID == tmdbEpisodeId);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByTmdbShowID(int showId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId).ToList());

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByTmdbEpisodeID(int episodeId)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId).OrderBy(a => a.Ordering).ToList());

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetAllByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(tmdbId).Concat(_anidbAnimeIDs!.GetMultiple(anidbId)).ToList());

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetOnlyByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetMultiple((anidbId, tmdbId)));

    protected override int SelectKey(CrossRef_AniDB_TMDB_Episode entity)
        => entity.CrossRef_AniDB_TMDB_EpisodeID;

    public override void PopulateIndexes()
    {
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _anidbEpisodeIDs = new(Cache, a => a.AnidbEpisodeID);
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _tmdbEpisodeIDs = new(Cache, a => a.TmdbEpisodeID);
        _pairedIDs = new(Cache, a => (a.AnidbAnimeID, a.TmdbShowID));
    }

    public override void RegenerateDb()
    {
    }

    public CrossRef_AniDB_TMDB_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
