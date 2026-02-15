using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_MessageRepository : BaseDirectRepository<AniDB_Message, int>
{
    public AniDB_Message GetByMessageId(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Message>()
                .Where(a => a.MessageID == id)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public List<AniDB_Message> GetUnhandledFileMoveMessages()
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Message>()
                .Where(a => a.Flags.HasFlag(AniDBMessageFlags.FileMoved) && !a.Flags.HasFlag(AniDBMessageFlags.FileMoveHandled))
                .ToList();
        });
    }

    public AniDB_MessageRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
