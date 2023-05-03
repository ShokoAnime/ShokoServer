using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_CharacterRepository : BaseDirectRepository<AniDB_Anime_Character, int>
{
    public List<AniDB_Anime_Character> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Character>()
                .Where(a => a.AnimeID == id)
                .ToList();
        });
    }

    public List<AniDB_Anime_Character> GetByCharID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Character>()
                .Where(a => a.CharID == id)
                .ToList();
        });
    }
}
