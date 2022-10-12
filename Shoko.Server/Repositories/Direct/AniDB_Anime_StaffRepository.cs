using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_StaffRepository : BaseDirectRepository<AniDB_Anime_Staff, int>
{
    public List<AniDB_Anime_Staff> GetByAnimeID(ISessionWrapper session, int id)
    {
        lock (GlobalDBLock)
        {
            var cats = session
                .CreateCriteria(typeof(AniDB_Anime_Staff))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Staff>();

            return new List<AniDB_Anime_Staff>(cats);
        }
    }

    public AniDB_Anime_Staff GetByAnimeIDAndCreatorID(int animeid, int creatorid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(AniDB_Anime_Staff))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("CreatorID", creatorid))
                .UniqueResult<AniDB_Anime_Staff>();

            return cr;
        }
    }
}
