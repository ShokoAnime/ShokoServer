#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_AlternateOrdering_SeasonRepository : BaseDirectRepository<TMDB_AlternateOrdering_Season, int>
{
    public IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbShowID == showId)
                .OrderBy(a => a.TmdbEpisodeGroupCollectionID)
                .ThenBy(e => e.SeasonNumber == 0)
                .ThenBy(e => e.SeasonNumber)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbEpisodeGroupCollectionID(string collectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId)
                .OrderBy(e => e.SeasonNumber == 0)
                .ThenBy(e => e.SeasonNumber)
                .ToList();
        });
    }

    public TMDB_AlternateOrdering_Season? GetByTmdbEpisodeGroupID(string groupId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbEpisodeGroupID == groupId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_AlternateOrdering_SeasonRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
