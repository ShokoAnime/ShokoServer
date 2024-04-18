using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_StaffRepository : BaseDirectRepository<AniDB_Anime_Staff, int>
{
    public List<AniDB_Anime_Staff> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Staff>()
                .Where(a => a.AnimeID == id)
                .ToList();
        });
    }
}
