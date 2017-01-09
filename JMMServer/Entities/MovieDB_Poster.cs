using System.IO;
using JMMServer.ImageDownload;
using JMMServer.Providers.MovieDB;
using NLog;
using Shoko.Models;

namespace JMMServer.Entities
{
    public class MovieDB_Poster : IImageEntity
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
                int pos = URL.IndexOf('/', 0);
                string fname = URL.Substring(pos + 1, URL.Length - pos - 1);
                fname = fname.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
            }
        }

        public void Populate(MovieDB_Image_Result searchResult, int movieID)
        {
            this.MovieId = movieID;
            this.ImageID = searchResult.ImageID;
            this.ImageType = searchResult.ImageType;
            this.ImageSize = searchResult.ImageSize;
            this.URL = searchResult.URL;
            this.ImageWidth = searchResult.ImageWidth;
            this.ImageHeight = searchResult.ImageHeight;
            this.Enabled = 1;
        }

        public Contract_MovieDB_Poster ToContract()
        {
            Contract_MovieDB_Poster contract = new Contract_MovieDB_Poster();
            contract.MovieDB_PosterID = this.MovieDB_PosterID;
            contract.ImageID = this.ImageID;
            contract.MovieId = this.MovieId;
            contract.ImageType = this.ImageType;
            contract.ImageSize = this.ImageSize;
            contract.URL = this.URL;
            contract.ImageWidth = this.ImageWidth;
            contract.ImageHeight = this.ImageHeight;
            contract.Enabled = this.Enabled;
            return contract;
        }
    }
}