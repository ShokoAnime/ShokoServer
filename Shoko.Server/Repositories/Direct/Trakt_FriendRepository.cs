using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class Trakt_FriendRepository : BaseDirectRepository<Trakt_Friend, int>
    {
        public Trakt_Friend GetByUsername(string username)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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