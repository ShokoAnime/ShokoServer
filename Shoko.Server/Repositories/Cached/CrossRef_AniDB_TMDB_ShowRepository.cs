using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_ShowRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_TMDB_Show, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _tmdbShowIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, (int, int)>? _pairedIDs;

    protected override int SelectKey(CrossRef_AniDB_TMDB_Show entity)
        => entity.CrossRef_AniDB_TMDB_ShowID;

    public override void PopulateIndexes()
    {
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _pairedIDs = new(Cache, a => (a.AnidbAnimeID, a.TmdbShowID));
    }

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByTmdbShowID(int showId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId));

    public CrossRef_AniDB_TMDB_Show? GetByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetOne((anidbId, tmdbId)));
}
