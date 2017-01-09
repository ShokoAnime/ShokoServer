using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class Trakt_SeasonRepository : BaseDirectRepository<Trakt_Season, int>
    {
        private Trakt_SeasonRepository()
        {
            
        }

        public static Trakt_SeasonRepository Create()
        {
            return new Trakt_SeasonRepository();
        }
        public List<Trakt_Season> GetByShowID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Season))
                    .Add(Restrictions.Eq("Trakt_ShowID", id))
                    .List<Trakt_Season>();

                return new List<Trakt_Season>(objs);
            }
        }

        public Trakt_Season GetByShowIDAndSeason(int id, int season)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByShowIDAndSeason(session, id, season);
            }
        }

        public Trakt_Season GetByShowIDAndSeason(ISession session, int id, int season)
        {
            Trakt_Season obj = session
                .CreateCriteria(typeof(Trakt_Season))
                .Add(Restrictions.Eq("Trakt_ShowID", id))
                .Add(Restrictions.Eq("Season", season))
                .UniqueResult<Trakt_Season>();

            return obj;
        }

    }
}