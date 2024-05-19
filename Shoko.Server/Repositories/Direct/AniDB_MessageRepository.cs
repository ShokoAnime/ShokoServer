using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_MessageRepository : BaseDirectRepository<AniDB_Message, int>
{
    public AniDB_Message GetByMessageId(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Message>()
                .Where(a => a.MessageID == id)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
