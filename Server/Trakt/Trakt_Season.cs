
namespace Shoko.Models.Server
{
    public class Trakt_Season
    {
        public Trakt_Season()
        {
        }
        public int Trakt_SeasonID { get; set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string URL { get; set; }
    }
}