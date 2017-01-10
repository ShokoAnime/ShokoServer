using Shoko.Models.Interfaces;

namespace Shoko.Models.Server
{
    public class MovieDB_Fanart : IImageEntity
    {
        public int MovieDB_FanartID { get; set; }
        public string ImageID { get; set; }
        public int MovieId { get; set; }
        public string ImageType { get; set; }
        public string ImageSize { get; set; }
        public string URL { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int Enabled { get; set; }

    }
}