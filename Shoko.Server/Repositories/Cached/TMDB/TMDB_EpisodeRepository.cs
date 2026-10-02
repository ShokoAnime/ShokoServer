using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Episode, int>(databaseFactory)
{
    protected override int SelectKey(TMDB_Episode entity) => entity.Id;
    private PocoIndex<int, TMDB_Episode, int> _showIDs = null!;
    private PocoIndex<int, TMDB_Episode, int> _seasonIDs = null!;
    private PocoIndex<int, TMDB_Episode, int> _episodeIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
        _seasonIDs = Cache.CreateIndex(a => a.TmdbSeasonID);
        _episodeIDs = Cache.CreateIndex(a => a.TmdbEpisodeID);
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbShowID(int showId)
    {
        return _showIDs
            .GetMultiple(showId)
            .OrderBy(e => e.SeasonNumber == 0)
            .ThenBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbSeasonID(int seasonId)
    {
        return _seasonIDs.GetMultiple(seasonId).OrderBy(a => a.EpisodeNumber).ToList();
    }

    /// <summary>
    ///   Looks up an episode whose show is stored. An episode left without its
    ///   show, as by an interrupted purge, is never handed out.
    /// </summary>
    /// <param name="episodeId">The TMDB episode ID.</param>
    /// <returns>The episode, or <c>null</c> when it or its show is missing.</returns>
    public TMDB_Episode? GetByTmdbEpisodeID(int episodeId)
        => _episodeIDs.GetOne(episodeId) is { } episode && RepoFactory.TMDB_Show.GetByTmdbShowID(episode.TmdbShowID) is not null
            ? episode
            : null;
}
