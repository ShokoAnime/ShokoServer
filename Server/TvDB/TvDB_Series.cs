

using System;

namespace Shoko.Models.Server
{
    public class TvDB_Series : ICloneable
    {
        public TvDB_Series()
        {
        }
        public int TvDB_SeriesID { get; set; }
        public int SeriesID { get; set; }
        public string Overview { get; set; }
        public string SeriesName { get; set; }
        public string Status { get; set; }
        public string Banner { get; set; }
        public string Fanart { get; set; }
        public string Lastupdated { get; set; }
        public string Poster { get; set; }

        public int? Rating { get; set; } // saved at * 10 to preserve decimal. resulting in 82/100

        public object Clone()
        {
            return new TvDB_Series
            {
                TvDB_SeriesID = TvDB_SeriesID,
                SeriesID = SeriesID,
                Overview = Overview,
                SeriesName = SeriesName,
                Status = Status,
                Banner = Banner,
                Fanart = Fanart,
                Lastupdated = Lastupdated,
                Poster = Poster,
                Rating = Rating
            };
        }
    }
}
