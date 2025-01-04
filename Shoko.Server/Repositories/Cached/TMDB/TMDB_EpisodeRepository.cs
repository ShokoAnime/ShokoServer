#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_EpisodeRepository : BaseCachedRepository<TMDB_Episode, int>
{
    protected override int SelectKey(TMDB_Episode entity) => entity.Id;
    private PocoIndex<int, TMDB_Episode, int> _showIDs = null!;
    private PocoIndex<int, TMDB_Episode, int> _seasonIDs = null!;
    private PocoIndex<int, TMDB_Episode, int> _episodeIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _seasonIDs = Cache.CreateIndex(a => a.TmdbSeasonID);
        _episodeIDs = Cache.CreateIndex(a => a.TMDB_EpisodeID);
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbShowID(int showId)
    {
        return _showIDs.GetMultiple(showId).OrderBy(a => a.EpisodeNumber).ToList();
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbSeasonID(int seasonId)
    {
        return _seasonIDs.GetMultiple(seasonId).OrderBy(a => a.EpisodeNumber).ToList();
    }

    public TMDB_Episode? GetByTmdbEpisodeID(int episodeId)
    {
        return _episodeIDs.GetOne(episodeId);
    }

    public TMDB_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
