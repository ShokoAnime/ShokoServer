using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class Trakt_ImagePosterRepository : BaseDirectRepository<Trakt_ImagePoster, int>
    {
        private Trakt_ImagePosterRepository()
        {
           
        }

        public static Trakt_ImagePosterRepository Create()
        {
            return new Trakt_ImagePosterRepository();
        }
        public List<Trakt_ImagePoster> GetByShowID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByShowID(session, id);
            }
        }

        public List<Trakt_ImagePoster> GetByShowID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(Trakt_ImagePoster))
                .Add(Restrictions.Eq("Trakt_ShowID", id))
                .List<Trakt_ImagePoster>();

            return new List<Trakt_ImagePoster>(objs);
        }

        public Trakt_ImagePoster GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Trakt_ImagePoster obj = session
                    .CreateCriteria(typeof(Trakt_ImagePoster))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .UniqueResult<Trakt_ImagePoster>();

                return obj;
            }
        }

    }
}