using JMMContracts;
using JMMServer.Providers.MovieDB;
using NLog;

namespace JMMServer.Entities
{
    public class MovieDB_Movie
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int MovieDB_MovieID { get; private set; }
        public int MovieId { get; set; }
        public string MovieName { get; set; }
        public string OriginalName { get; set; }
        public string Overview { get; set; }

        public void Populate(MovieDB_Movie_Result searchResult)
        {
            MovieId = searchResult.MovieID;
            MovieName = searchResult.MovieName;
            OriginalName = searchResult.OriginalName;
            Overview = searchResult.Overview;
        }

        public Contract_MovieDB_Movie ToContract()
        {
            var contract = new Contract_MovieDB_Movie();
            contract.MovieId = MovieId;
            contract.MovieName = MovieName;
            contract.OriginalName = OriginalName;
            contract.Overview = Overview;
            contract.MovieDB_MovieID = MovieDB_MovieID;
            return contract;
        }
    }
}