using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_SeasonRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Season, int>(databaseFactory)
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

    /// <summary>
    ///   Looks up a season whose show is stored. A season left without its
    ///   show, as by an interrupted purge, is never handed out.
    /// </summary>
    /// <param name="tmdbSeasonId">The TMDB season ID.</param>
    /// <returns>The season, or <c>null</c> when it or its show is missing.</returns>
    public TMDB_Season? GetByTmdbSeasonID(int tmdbSeasonId)
        => _seasonIDs.GetOne(tmdbSeasonId) is { } season && RepoFactory.TMDB_Show.GetByTmdbShowID(season.TmdbShowID) is not null
            ? season
            : null;
}
