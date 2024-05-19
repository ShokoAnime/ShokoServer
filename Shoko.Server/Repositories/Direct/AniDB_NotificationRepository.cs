using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_NotificationRepository : BaseDirectRepository<AniDB_Notification, int>
{
    public AniDB_Notification GetByNotificationId(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Notification>()
                .Where(a => a.NotificationID == id)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
