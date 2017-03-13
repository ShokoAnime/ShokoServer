using System.Collections.Generic;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class Trakt_ImageFanartRepository : BaseDirectRepository<Trakt_ImageFanart, int>
    {
        private Trakt_ImageFanartRepository()
        {
        }

        public static Trakt_ImageFanartRepository Create()
        {
            return new Trakt_ImageFanartRepository();
        }

        public List<Trakt_ImageFanart> GetByShowID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByShowID(session, id);
            }
        }

        public List<Trakt_ImageFanart> GetByShowID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(Trakt_ImageFanart))
                .Add(Restrictions.Eq("Trakt_ShowID", id))
                .List<Trakt_ImageFanart>();

            return new List<Trakt_ImageFanart>(objs);
        }

        public Trakt_ImageFanart GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Trakt_ImageFanart obj = session
                    .CreateCriteria(typeof(Trakt_ImageFanart))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .UniqueResult<Trakt_ImageFanart>();

                return obj;
            }
        }
    }
}