using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Repositories.Direct;

public class VersionsRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<Versions, int>(databaseFactory)
{
    public Dictionary<(string, string), Versions> GetAllByType(string vertype)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<Versions>()
                .Where(a => a.VersionType == vertype).ToList()
                .GroupBy(a => (a.VersionValue ?? string.Empty, a.VersionRevision ?? string.Empty))
                .ToDictionary(a => a.Key, a => a.FirstOrDefault());
        });
    }
}
