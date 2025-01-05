#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_AlternateOrdering_EpisodeRepository : BaseDirectRepository<TMDB_AlternateOrdering_Episode, int>
{
    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Episode>()
                .Where(a => a.TmdbShowID == showId)
                .OrderBy(a => a.TmdbEpisodeGroupCollectionID)
                .ThenBy(e => e.SeasonNumber == 0)
                .ThenBy(e => e.SeasonNumber)
                .ThenBy(xref => xref.EpisodeNumber)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetByTmdbEpisodeGroupCollectionID(string collectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Episode>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId)
                .OrderBy(e => e.SeasonNumber == 0)
                .ThenBy(e => e.SeasonNumber)
                .ThenBy(xref => xref.EpisodeNumber)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetByTmdbEpisodeGroupID(string groupId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Episode>()
                .Where(a => a.TmdbEpisodeGroupID == groupId)
                .OrderBy(xref => xref.EpisodeNumber)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetByTmdbEpisodeID(int episodeId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Episode>()
                .Where(a => a.TmdbEpisodeID == episodeId)
                .OrderBy(a => a.TmdbEpisodeGroupID)
                .ToList();
        });
    }

    public TMDB_AlternateOrdering_Episode? GetByEpisodeGroupCollectionAndEpisodeIDs(string collectionId, int episodeId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Episode>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId && a.TmdbEpisodeID == episodeId)
                .OrderBy(a => a.SeasonNumber)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_AlternateOrdering_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
