using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    public class SVR_CrossRef_AniDB_Other : CrossRef_AniDB_Other
    {
        public MovieDB_Movie GetMovieDB_Movie()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetMovieDB_Movie(session.Wrap());
            }
        }

        public MovieDB_Movie GetMovieDB_Movie(ISessionWrapper session)
        {
            if (CrossRefType != (int) JMMServer.CrossRefType.MovieDB)
                return null;
            return RepoFactory.MovieDb_Movie.GetByOnlineID(session, int.Parse(CrossRefID));
        }

    }
}