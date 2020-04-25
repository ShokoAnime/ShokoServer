
using System;
using Shoko.Models.Interfaces;

namespace Shoko.Models.Server
{
    public class TvDB_ImageFanart : IImageEntity, ICloneable
    {
        public int TvDB_ImageFanartID { get; set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Colors { get; set; }
        public string Language { get; set; }
        public string VignettePath { get; set; }
        public int Enabled { get; set; }
        public int Chosen { get; set; }
        public object Clone()
        {
            return new TvDB_ImageFanart
            {
                TvDB_ImageFanartID = TvDB_ImageFanartID,
                Id = Id,
                SeriesID = SeriesID,
                BannerPath = BannerPath,
                BannerType = BannerType,
                BannerType2 = BannerType2,
                Colors = Colors,
                Language = Language,
                VignettePath = VignettePath,
                Enabled = Enabled,
                Chosen = Chosen
            };
        }
    }
}
