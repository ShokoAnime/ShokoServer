using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_TMDB_Episode, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _anidbEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _tmdbShowIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, int>? _tmdbEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Episode, (int, int)>? _pairedIDs;

    protected override int SelectKey(CrossRef_AniDB_TMDB_Episode entity)
        => entity.CrossRef_AniDB_TMDB_EpisodeID;

    public override void PopulateIndexes()
    {
        _anidbAnimeIDs = Cache.CreateIndex(a => a.AnidbAnimeID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeID);
        _tmdbShowIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _tmdbEpisodeIDs = Cache.CreateIndex(a => a.TmdbEpisodeID);
        _pairedIDs = Cache.CreateIndex(a => (a.AnidbAnimeID, a.TmdbShowID));
    }

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
        => GetByTmdbShowID(tmdbId).Concat(GetByAnidbAnimeID(anidbId)).ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetOnlyByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetMultiple((anidbId, tmdbId)));
}
