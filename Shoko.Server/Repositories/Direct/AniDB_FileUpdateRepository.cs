using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_FileUpdateRepository : BaseDirectRepository<AniDB_FileUpdate, int>
{
    public IList<AniDB_FileUpdate> GetByFileSizeAndHash(long fileSize, string hash)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cats = session
                .CreateCriteria(typeof(AniDB_FileUpdate))
                .Add(Restrictions.Eq("FileSize", fileSize))
                .Add(Restrictions.Eq("Hash", hash))
                .AddOrder(Order.Desc("UpdatedAt"))
                .List<AniDB_FileUpdate>();

            return cats;
        }
    }

    public AniDB_FileUpdate GetLastUpdateByFileSizeAndHash(long fileSize, string hash)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cat = session
                .CreateCriteria(typeof(AniDB_FileUpdate))
                .Add(Restrictions.Eq("FileSize", fileSize))
                .Add(Restrictions.Eq("Hash", hash))
                .AddOrder(Order.Desc("UpdatedAt"))
                .SetMaxResults(1)
                .UniqueResult<AniDB_FileUpdate>();

            return cat;
        }
    }
}
