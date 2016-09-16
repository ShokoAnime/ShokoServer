using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class MovieDb_MovieRepository : BaseDirectRepository<MovieDB_Movie, int>
    {

        public MovieDB_Movie GetByOnlineID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session.Wrap(), id);
            }
        }

        public MovieDB_Movie GetByOnlineID(ISessionWrapper session, int id)
        {
            MovieDB_Movie cr = session
                .CreateCriteria(typeof(MovieDB_Movie))
                .Add(Restrictions.Eq("MovieId", id))
                .UniqueResult<MovieDB_Movie>();
            return cr;
        }
    }
}