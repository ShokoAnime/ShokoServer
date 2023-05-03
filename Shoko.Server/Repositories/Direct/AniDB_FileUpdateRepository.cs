using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_FileUpdateRepository : BaseDirectRepository<AniDB_FileUpdate, int>
{
    public IList<AniDB_FileUpdate> GetByFileSizeAndHash(long fileSize, string hash)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_FileUpdate>()
                .Where(a => a.Hash == hash && a.FileSize == fileSize)
                .OrderByDescending(a => a.UpdatedAt)
                .ToList();
        });
    }
}
