

namespace Shoko.Models.Server
{
    public class MovieDB_Movie
    {
        public MovieDB_Movie()
        {
        }
        public int MovieDB_MovieID { get; set; }
        public int MovieId { get; set; }
        public string MovieName { get; set; }
        public string OriginalName { get; set; }
        public string Overview { get; set; }
        public int Rating { get; set; } // saved at * 10 to preserve decimal. resulting in 82/100

    }
}