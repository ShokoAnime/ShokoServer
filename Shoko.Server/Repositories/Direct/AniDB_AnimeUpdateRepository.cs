using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_AnimeUpdateRepository : BaseDirectRepository<AniDB_AnimeUpdate, int>
{
    public AniDB_AnimeUpdate GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            var cats = session.Query<AniDB_AnimeUpdate>()
                .Where(a => a.AnimeID == id)
                .OrderByDescending(a => a.UpdatedAt).ToList();

            var cat = cats.FirstOrDefault();
            cats.Remove(cat);
            if (cats.Count > 1)
            {
                cats.ForEach(Delete);
            }

            return cat;
        });
    }

    public AniDB_AnimeUpdateRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
