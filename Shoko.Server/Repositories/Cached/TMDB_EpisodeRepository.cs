using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class TMDB_EpisodeRepository : BaseCachedRepository<TMDB_Episode, int>
{
    private PocoIndex<int, TMDB_Episode, int> TmdbShowIds;
    private PocoIndex<int, TMDB_Episode, int> TmdbSeasonIds;
    private PocoIndex<int, TMDB_Episode, int> TmdbEpisodeIds;

    public override void PopulateIndexes()
    {
        TmdbShowIds = new PocoIndex<int, TMDB_Episode, int>(Cache, a => a.TmdbShowID);
        TmdbSeasonIds = new PocoIndex<int, TMDB_Episode, int>(Cache, a => a.TmdbSeasonID);
        TmdbEpisodeIds = new PocoIndex<int, TMDB_Episode, int>(Cache, a => a.TmdbEpisodeID);
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbShowID(int showId)
    {
        return ReadLock(() =>
        {
            return TmdbShowIds
                .GetMultiple(showId)
                .OrderBy(a => a.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbSeasonID(int seasonId)
    {
        return ReadLock(() =>
        {
            return TmdbSeasonIds
                .GetMultiple(seasonId)
                .OrderBy(e => e.EpisodeNumber)
                .ToList();
        });
    }

    public TMDB_Episode? GetByTmdbEpisodeID(int episodeId)
    {
        return ReadLock(() => TmdbEpisodeIds.GetOne(episodeId));
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(TMDB_Episode entity)
    {
        return entity.TMDB_EpisodeID;
    }

    public TMDB_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
