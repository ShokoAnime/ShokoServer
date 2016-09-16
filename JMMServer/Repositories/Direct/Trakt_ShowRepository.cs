using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class Trakt_ShowRepository : BaseDirectRepository<Trakt_Show, int>
    {
        private Trakt_ShowRepository()
        {
            
        }

        public static Trakt_ShowRepository Create()
        {
            return new Trakt_ShowRepository();
        }
        public Trakt_Show GetByTraktSlug(string slug)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTraktSlug(session, slug);
            }
        }


        public Trakt_Show GetByTraktSlug(ISession session, string slug)
        {
            Trakt_Show cr = session
                .CreateCriteria(typeof(Trakt_Show))
                .Add(Restrictions.Eq("TraktID", slug))
                .UniqueResult<Trakt_Show>();
            return cr;
        }

    }
}