using JMMContracts;
using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
using NHibernate;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_Other
    {
        public int CrossRef_AniDB_OtherID { get; private set; }
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }

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

        public Contract_CrossRef_AniDB_Other ToContract()
        {
            Contract_CrossRef_AniDB_Other contract = new Contract_CrossRef_AniDB_Other();
            contract.AnimeID = this.AnimeID;
            contract.CrossRefID = this.CrossRefID;
            contract.CrossRefSource = this.CrossRefSource;
            contract.CrossRefType = this.CrossRefType;
            return contract;
        }
    }
}