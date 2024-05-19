using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_NotifyQueueRepository : BaseDirectRepository<AniDB_NotifyQueue, int>
{

    public AniDB_NotifyQueue GetByTypeID(AniDBNotifyType type, int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_NotifyQueue>()
                .Where(a => a.Type == type && a.ID == id)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public List<AniDB_NotifyQueue> GetByType(AniDBNotifyType type)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_NotifyQueue>()
                .Where(a => a.Type == type)
                .ToList();
        });
    }

    public void DeleteForTypeID(AniDBNotifyType type, int id)
    {
        Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            // Query can't batch delete, while Query can
            session.Query<AniDB_NotifyQueue>().Where(a => a.Type == type && a.ID == id).Delete();
        });
    }
}
