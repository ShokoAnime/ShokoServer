using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_ShowRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Show, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _anidbAnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _tmdbShowIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, (int, int)>? _pairedIDs;

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByTmdbShowID(int showId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId));

    public CrossRef_AniDB_TMDB_Show? GetByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetOne((anidbId, tmdbId)));

    public ILookup<int, CrossRef_AniDB_TMDB_Show> GetByAnimeIDsAndType(IReadOnlyCollection<int> animeIds)
    {
        if (animeIds == null || animeIds?.Count == 0)
            return EmptyLookup<int, CrossRef_AniDB_TMDB_Show>.Instance;

        return Lock(
            () => animeIds!.SelectMany(animeId => _anidbAnimeIDs!.GetMultiple(animeId)).ToLookup(xref => xref.AnidbAnimeID)
        );
    }

    protected override int SelectKey(CrossRef_AniDB_TMDB_Show entity)
        => entity.CrossRef_AniDB_TMDB_ShowID;

    public override void PopulateIndexes()
    {
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _pairedIDs = new(Cache, a => (a.AnidbAnimeID, a.TmdbShowID));
    }

    public override void RegenerateDb()
    {
    }

    public CrossRef_AniDB_TMDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
