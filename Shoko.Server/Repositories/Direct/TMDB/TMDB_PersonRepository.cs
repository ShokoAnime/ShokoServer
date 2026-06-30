using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct.TMDB;

public class TMDB_PersonRepository : BaseDirectRepository<TMDB_Person, int>
{
    public TMDB_Person? GetByTmdbPersonID(int creditId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Person>()
            .Where(a => a.TmdbPersonID == creditId)
            .Take(1)
            .SingleOrDefault();
    }

    public HashSet<int> GetExistingTmdbPersonIDs(IReadOnlyCollection<int> personIds)
    {
        if (personIds.Count == 0) return [];
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Person>()
            .Where(a => personIds.Contains(a.TmdbPersonID))
            .Select(a => a.TmdbPersonID)
            .ToHashSet();
    }

    public TMDB_PersonRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
