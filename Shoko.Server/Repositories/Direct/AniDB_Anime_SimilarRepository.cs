using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_SimilarRepository : BaseDirectRepository<AniDB_Anime_Similar, int>
{
    public AniDB_Anime_Similar GetByAnimeIDAndSimilarID(int animeid, int similaranimeid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Similar>()
                .Where(a => a.AnimeID == animeid && a.SimilarAnimeID == similaranimeid)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public List<AniDB_Anime_Similar> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Similar>()
                .Where(a => a.AnimeID == id)
                .OrderByDescending(a => a.Approval)
                .ToList();
        });
    }
}
