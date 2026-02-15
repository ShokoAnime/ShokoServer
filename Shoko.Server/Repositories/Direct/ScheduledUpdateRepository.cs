using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Repositories.Direct;

public class ScheduledUpdateRepository : BaseDirectRepository<ScheduledUpdate, int>
{
    public ScheduledUpdate GetByUpdateType(int uptype)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ScheduledUpdate>()
                .Where(a => a.UpdateType == uptype)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public ScheduledUpdateRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
