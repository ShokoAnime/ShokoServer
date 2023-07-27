using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Character_SeiyuuRepository : BaseDirectRepository<AniDB_Character_Seiyuu, int>
{
    public AniDB_Character_Seiyuu GetByCharIDAndSeiyuuID(int charID, int seiyuuID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Character_Seiyuu>()
                .Where(a => a.CharID == charID && a.SeiyuuID == seiyuuID)
                .SingleOrDefault();
        });
    }

    public List<AniDB_Character_Seiyuu> GetByCharID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return GetByCharIDUnsafe(session.Wrap(), id);
        });
    }

    public List<AniDB_Character_Seiyuu> GetByCharID(ISession session, int id)
    {
        return Lock(() => GetByCharIDUnsafe(session.Wrap(), id));
    }

    private static List<AniDB_Character_Seiyuu> GetByCharIDUnsafe(ISessionWrapper session, int id)
    {
        return session.Query<AniDB_Character_Seiyuu>()
            .Where(a => a.CharID == id)
            .ToList();
    }

    public List<AniDB_Character_Seiyuu> GetBySeiyuuID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Character_Seiyuu>()
                .Where(a => a.SeiyuuID == id)
                .ToList();
        });
    }
}
