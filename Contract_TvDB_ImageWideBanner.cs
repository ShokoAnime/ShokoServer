namespace Shoko.Models
{
    public class Contract_TvDB_ImageWideBanner
    {
        public int TvDB_ImageWideBannerID { get; set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Language { get; set; }
        public int Enabled { get; set; }
        public int? SeasonNumber { get; set; }
    }
}