
using Shoko.Models.Interfaces;


namespace Shoko.Models.Server
{
    public class Trakt_ImagePoster : IImageEntity
    {
        public Trakt_ImagePoster()
        {
        }

        public int Trakt_ImagePosterID { get; set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string ImageURL { get; set; }
        public int Enabled { get; set; }


    }
}