using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_AnimeUpdateRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<AniDB_AnimeUpdate, int>(databaseFactory)
{
    public AniDB_AnimeUpdate? GetByAnimeID(int id)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        var cats = session.Query<AniDB_AnimeUpdate>()
            .Where(a => a.AnimeID == id)
            .OrderByDescending(a => a.UpdatedAt).ToList();

        var cat = cats.FirstOrDefault();
        if (cat is not null)
            cats.Remove(cat);
        if (cats.Count > 1)
        {
            cats.ForEach(Delete);
        }

        return cat;
    }
}
