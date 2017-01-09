using System.IO;
using NLog;
using Shoko.Models;
using Shoko.Models.Interfaces;
using Shoko.Server.ImageDownload;

namespace Shoko.Server.Entities
{
    public class Trakt_ImagePoster : IImageEntity
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int Trakt_ImagePosterID { get; private set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string ImageURL { get; set; }
        public int Enabled { get; set; }

        public string FullImagePath
        {
            get
            {
                // typical url
                // http://vicmackey.trakt.tv/images/seasons/3228-1.jpg
                // http://vicmackey.trakt.tv/images/posters/1130.jpg

                if (string.IsNullOrEmpty(ImageURL)) return "";

                int pos = ImageURL.IndexOf(@"images/");
                if (pos <= 0) return "";

                string relativePath = ImageURL.Substring(pos + 7, ImageURL.Length - pos - 7);
                relativePath = relativePath.Replace("/", @"\");

                return Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
            }
        }

        public Contract_Trakt_ImagePoster ToContract()
        {
            Contract_Trakt_ImagePoster contract = new Contract_Trakt_ImagePoster();
            contract.Trakt_ImagePosterID = this.Trakt_ImagePosterID;
            contract.Trakt_ShowID = this.Trakt_ShowID;
            contract.Season = this.Season;
            contract.ImageURL = this.ImageURL;
            contract.Enabled = this.Enabled;

            return contract;
        }
    }
}