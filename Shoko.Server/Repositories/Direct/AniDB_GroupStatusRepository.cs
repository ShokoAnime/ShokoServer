using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    public List<AniDB_GroupStatus> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_GroupStatus>()
                .Where(a => a.AnimeID == id)
                .ToList();
        });
    }

    public void DeleteForAnime(int animeid)
    {
        Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            // Query can't batch delete, while Query can
            session.Query<AniDB_GroupStatus>().Where(a => a.AnimeID == animeid).Delete();
        });

        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeid);
    }
}
