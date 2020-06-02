using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class IgnoreAnimeRepository : BaseDirectRepository<IgnoreAnime, int>
    {
        public IgnoreAnime GetByAnimeUserType(int animeID, int userID, int ignoreType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                IgnoreAnime obj = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .Add(Restrictions.Eq("IgnoreType", ignoreType))
                    .UniqueResult<IgnoreAnime>();

                return obj;
            }
        }

        public List<IgnoreAnime> GetByUserAndType(int userID, int ignoreType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .Add(Restrictions.Eq("IgnoreType", ignoreType))
                    .List<IgnoreAnime>();

                return new List<IgnoreAnime>(objs);
            }
        }

        public List<IgnoreAnime> GetByUser(int userID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .List<IgnoreAnime>();

                return new List<IgnoreAnime>(objs);
            }
        }
    }
}