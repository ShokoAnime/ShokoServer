using System.IO;


namespace Shoko.Models.Server
{
    public class Trakt_Episode
    {
        public Trakt_Episode()
        {
        }
        public int Trakt_EpisodeID { get; set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }
        public string Title { get; set; }
        public string URL { get; set; }
        public string Overview { get; set; }
        public string EpisodeImage { get; set; }
        public int? TraktID { get; set; }


    }
}