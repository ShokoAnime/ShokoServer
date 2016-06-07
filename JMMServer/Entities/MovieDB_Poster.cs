using System.IO;
using JMMContracts;
using JMMServer.ImageDownload;
using JMMServer.Providers.MovieDB;
using NLog;

namespace JMMServer.Entities
{
    public class MovieDB_Poster
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int MovieDB_PosterID { get; private set; }
        public string ImageID { get; set; }
        public int MovieId { get; set; }
        public string ImageType { get; set; }
        public string ImageSize { get; set; }
        public string URL { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int Enabled { get; set; }


        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(URL)) return "";

                //strip out the base URL
                var pos = URL.IndexOf('/', 0);
                var fname = URL.Substring(pos + 1, URL.Length - pos - 1);
                fname = fname.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
            }
        }

        public void Populate(MovieDB_Image_Result searchResult, int movieID)
        {
            MovieId = movieID;
            ImageID = searchResult.ImageID;
            ImageType = searchResult.ImageType;
            ImageSize = searchResult.ImageSize;
            URL = searchResult.URL;
            ImageWidth = searchResult.ImageWidth;
            ImageHeight = searchResult.ImageHeight;
            Enabled = 1;
        }

        public Contract_MovieDB_Poster ToContract()
        {
            var contract = new Contract_MovieDB_Poster();
            contract.MovieDB_PosterID = MovieDB_PosterID;
            contract.ImageID = ImageID;
            contract.MovieId = MovieId;
            contract.ImageType = ImageType;
            contract.ImageSize = ImageSize;
            contract.URL = URL;
            contract.ImageWidth = ImageWidth;
            contract.ImageHeight = ImageHeight;
            contract.Enabled = Enabled;
            return contract;
        }
    }
}