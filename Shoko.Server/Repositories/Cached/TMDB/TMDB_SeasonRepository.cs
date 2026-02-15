using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_SeasonRepository : BaseCachedRepository<TMDB_Season, int>
{
    protected override int SelectKey(TMDB_Season entity) => entity.Id;
    private PocoIndex<int, TMDB_Season, int> _showIDs = null!;
    private PocoIndex<int, TMDB_Season, int> _seasonIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _seasonIDs = Cache.CreateIndex(a => a.TmdbSeasonID);
    }

    public IReadOnlyList<TMDB_Season> GetByTmdbShowID(int tmdbShowId)
    {
        return _showIDs.GetMultiple(tmdbShowId)
            .OrderBy(e => e.SeasonNumber == 0)
            .ThenBy(e => e.SeasonNumber)
            .ToList();
    }

    public TMDB_Season? GetByTmdbSeasonID(int tmdbSeasonId)
    {
        return _seasonIDs.GetOne(tmdbSeasonId);
    }

    public TMDB_SeasonRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
