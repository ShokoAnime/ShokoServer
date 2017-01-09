using NLog;
using Shoko.Models;
using Shoko.Server.Providers.MovieDB;

namespace Shoko.Server.Entities
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
            this.MovieId = searchResult.MovieID;
            this.MovieName = searchResult.MovieName;
            this.OriginalName = searchResult.OriginalName;
            this.Overview = searchResult.Overview;
        }

        public Contract_MovieDB_Movie ToContract()
        {
            Contract_MovieDB_Movie contract = new Contract_MovieDB_Movie();
            contract.MovieId = this.MovieId;
            contract.MovieName = this.MovieName;
            contract.OriginalName = this.OriginalName;
            contract.Overview = this.Overview;
            contract.MovieDB_MovieID = this.MovieDB_MovieID;
            return contract;
        }
    }
}