using JMMContracts;
using JMMServer.Repositories;
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
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetMovieDB_Movie(session);
            }
        }

        public MovieDB_Movie GetMovieDB_Movie(ISession session)
        {
            if (CrossRefType != (int)JMMServer.CrossRefType.MovieDB)
                return null;
            var repMovieDBMovie = new MovieDB_MovieRepository();
            return repMovieDBMovie.GetByOnlineID(session, int.Parse(CrossRefID));
        }

        public Contract_CrossRef_AniDB_Other ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_Other();
            contract.AnimeID = AnimeID;
            contract.CrossRefID = CrossRefID;
            contract.CrossRefSource = CrossRefSource;
            contract.CrossRefType = CrossRefType;
            return contract;
        }
    }
}