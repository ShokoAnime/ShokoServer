
using Shoko.Models.Interfaces;


namespace Shoko.Models.Server
{
    public class Trakt_ImageFanart : IImageEntity
    {
        public Trakt_ImageFanart()
        {
        }

        public int Trakt_ImageFanartID { get; set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string ImageURL { get; set; }
        public int Enabled { get; set; }

    }
}