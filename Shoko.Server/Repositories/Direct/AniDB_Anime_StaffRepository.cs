using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_StaffRepository : BaseDirectRepository<AniDB_Anime_Staff, int>
{
    public List<AniDB_Anime_Staff> GetByAnimeID(int animeID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Staff>()
                .Where(a => a.AnimeID == animeID)
                .ToList();
        });
    }

    public List<AniDB_Anime_Staff> GetByCreatorID(int creatorID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Staff>()
                .Where(a => a.CreatorID == creatorID)
                .ToList();
        });
    }

    public AniDB_Anime_StaffRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
