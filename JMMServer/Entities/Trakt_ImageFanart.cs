using System.IO;
using JMMServer.ImageDownload;
using NLog;
using Shoko.Models;

namespace JMMServer.Entities
{
    public class Trakt_ImageFanart : IImageEntity
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int Trakt_ImageFanartID { get; private set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string ImageURL { get; set; }
        public int Enabled { get; set; }

        public string FullImagePath
        {
            get
            {
                // typical url
                // http://vicmackey.trakt.tv/images/fanart/3228.jpg

                if (string.IsNullOrEmpty(ImageURL)) return "";

                int pos = ImageURL.IndexOf(@"images/");
                if (pos <= 0) return "";

                string relativePath = ImageURL.Substring(pos + 7, ImageURL.Length - pos - 7);
                relativePath = relativePath.Replace("/", @"\");

                return Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
            }
        }

        public Contract_Trakt_ImageFanart ToContract()
        {
            Contract_Trakt_ImageFanart contract = new Contract_Trakt_ImageFanart();
            contract.Trakt_ImageFanartID = this.Trakt_ImageFanartID;
            contract.Trakt_ShowID = this.Trakt_ShowID;
            contract.Season = this.Season;
            contract.ImageURL = this.ImageURL;
            contract.Enabled = this.Enabled;

            return contract;
        }
    }
}