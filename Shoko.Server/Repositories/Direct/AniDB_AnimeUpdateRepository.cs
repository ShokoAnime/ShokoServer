using System.Linq;
using NHibernate.Criterion;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_AnimeUpdateRepository : BaseDirectRepository<AniDB_AnimeUpdate, int>
{
    public AniDB_AnimeUpdate GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cats = session
                .CreateCriteria(typeof(AniDB_AnimeUpdate))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_AnimeUpdate>();

            var cat = cats.FirstOrDefault();
            cats.Remove(cat);
            if (cats.Count > 1)
            {
                cats.ForEach(Delete);
            }

            return cat;
        });
    }
}
