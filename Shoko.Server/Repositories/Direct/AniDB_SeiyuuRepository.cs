using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_SeiyuuRepository : BaseDirectRepository<AniDB_Seiyuu, int>
{
    public AniDB_Seiyuu GetBySeiyuuID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Seiyuu>()
                .Where(a => a.SeiyuuID == id)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public AniDB_Seiyuu GetBySeiyuuID(ISessionWrapper session, int id)
    {
        return Lock(() => session.Query<AniDB_Seiyuu>()
            .Where(a => a.SeiyuuID == id)
            .Take(1)
            .SingleOrDefault());
    }
}
