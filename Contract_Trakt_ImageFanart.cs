namespace Shoko.Models
{
    public class Contract_Trakt_ImageFanart
    {
        public int Trakt_ImageFanartID { get; set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string ImageURL { get; set; }
        public int Enabled { get; set; }
    }
}