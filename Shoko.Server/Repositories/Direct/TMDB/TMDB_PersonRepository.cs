using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_PersonRepository : BaseDirectRepository<TMDB_Person, int>
{
    public TMDB_Person? GetByTmdbPersonID(int creditId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Person>()
                .Where(a => a.TmdbPersonID == creditId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_PersonRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
