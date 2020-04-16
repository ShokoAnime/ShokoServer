
using System;
using Shoko.Models.Interfaces;


namespace Shoko.Models.Server
{
    public class TvDB_ImageWideBanner : IImageEntity, ICloneable
    {
        public TvDB_ImageWideBanner()
        {
        }
        public int TvDB_ImageWideBannerID { get; set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Language { get; set; }
        public int Enabled { get; set; }
        public int? SeasonNumber { get; set; }
        public object Clone()
        {
            return new TvDB_ImageWideBanner
            {
                TvDB_ImageWideBannerID = TvDB_ImageWideBannerID,
                Id = Id,
                SeriesID = SeriesID,
                BannerPath = BannerPath,
                BannerType = BannerType,
                BannerType2 = BannerType2,
                Language = Language,
                Enabled = Enabled,
                SeasonNumber = SeasonNumber
            };
        }
    }
}
