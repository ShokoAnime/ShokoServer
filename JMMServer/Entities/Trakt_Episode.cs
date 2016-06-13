using System.IO;
using JMMContracts;
using JMMServer.ImageDownload;
using NLog;

namespace JMMServer.Entities
{
    public class Trakt_Episode
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int Trakt_EpisodeID { get; private set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }
        public string Title { get; set; }
        public string URL { get; set; }
        public string Overview { get; set; }
        public string EpisodeImage { get; set; }
        public int? TraktID { get; set; }

        public string FullImagePath
        {
            get
            {
                // typical EpisodeImage url
                // http://vicmackey.trakt.tv/images/episodes/3228-1-1.jpg

                // get the TraktID from the URL
                // http://trakt.tv/show/11eyes/season/1/episode/1 (11 eyes)

                if (string.IsNullOrEmpty(EpisodeImage)) return "";
                if (string.IsNullOrEmpty(URL)) return "";

                // on Trakt, if the episode doesn't have a proper screenshot, they will return the
                // fanart instead, we will ignore this
                int pos = EpisodeImage.IndexOf(@"episodes/");
                if (pos <= 0) return "";

                int posID = URL.IndexOf(@"show/");
                if (posID <= 0) return "";

                int posIDNext = URL.IndexOf(@"/", posID + 6);
                if (posIDNext <= 0) return "";

                string traktID = URL.Substring(posID + 5, posIDNext - posID - 5);
                traktID = traktID.Replace("/", @"\");

                string imageName = EpisodeImage.Substring(pos + 9, EpisodeImage.Length - pos - 9);
                imageName = imageName.Replace("/", @"\");

                string relativePath = Path.Combine("episodes", traktID);
                relativePath = Path.Combine(relativePath, imageName);

                return Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
            }
        }

        public Contract_Trakt_Episode ToContract()
        {
            Contract_Trakt_Episode contract = new Contract_Trakt_Episode();

            contract.Trakt_EpisodeID = Trakt_EpisodeID;
            contract.Trakt_ShowID = Trakt_ShowID;
            contract.Season = Season;
            contract.EpisodeNumber = EpisodeNumber;
            contract.Title = Title;
            contract.URL = URL;
            contract.Overview = Overview;
            contract.EpisodeImage = EpisodeImage;

            return contract;
        }
    }
}