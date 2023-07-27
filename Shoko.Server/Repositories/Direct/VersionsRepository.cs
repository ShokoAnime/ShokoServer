using System.Collections.Generic;
using System.Linq;
using Shoko.Models;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class VersionsRepository : BaseDirectRepository<Versions, int>
{
    public Dictionary<(string, string), Versions> GetAllByType(string vertype)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<Versions>()
                .Where(a => a.VersionType == vertype).ToList()
                .GroupBy(a => (a.VersionValue ?? string.Empty, a.VersionRevision ?? string.Empty))
                .ToDictionary(a => a.Key, a => a.FirstOrDefault());
        });
    }
}
