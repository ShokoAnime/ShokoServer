using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_AnimeUpdateRepository : BaseDirectRepository<AniDB_AnimeUpdate, int>
    {
        private AniDB_AnimeUpdateRepository()
        {
        }

        public static AniDB_AnimeUpdateRepository Create()
        {
            return new AniDB_AnimeUpdateRepository();
        }

        public AniDB_AnimeUpdate GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(AniDB_AnimeUpdate))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_AnimeUpdate>();

                var cat = cats.FirstOrDefault();
                cats.Remove(cat);
                if (cats.Count > 1) cats.ForEach(Delete);

                return cat;
            }
        }

        public AniDB_AnimeUpdate GetByAnimeID(ISessionWrapper session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(AniDB_AnimeUpdate))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_AnimeUpdate>();

            var cat = cats.FirstOrDefault();
            cats.Remove(cat);
            if (cats.Count > 1) cats.ForEach(Delete);

            return cat;
        }
    }
}