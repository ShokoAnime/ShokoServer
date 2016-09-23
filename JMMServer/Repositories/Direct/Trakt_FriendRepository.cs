using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class Trakt_FriendRepository : BaseDirectRepository<Trakt_Friend, int>
    {
        public Trakt_FriendRepository()
        {
            
        }

        public static Trakt_FriendRepository Create()
        {
            return new Trakt_FriendRepository();
        }
        public Trakt_Friend GetByUsername(string username)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByUsername(session, username);
            }
        }

        public Trakt_Friend GetByUsername(ISession session, string username)
        {
            Trakt_Friend obj = session
                .CreateCriteria(typeof(Trakt_Friend))
                .Add(Restrictions.Eq("Username", username))
                .UniqueResult<Trakt_Friend>();

            return obj;
        }

    }
}