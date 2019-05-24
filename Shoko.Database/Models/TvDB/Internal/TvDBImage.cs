using System.ComponentModel.DataAnnotations;

namespace Shoko.Database.Models.TvDB.Internal
{
    public class TvDBImage
    {
        public int Id { get; set; }
        public int SeriesID { get; set; } //FK to an Index
        [MaxLength(200)] public string BannerPath { get; set; }
        [MaxLength(200)] public string BannerType { get; set; }
        [MaxLength(200)] public string BannerType2 { get; set; }
        [MaxLength(200)] public string Language { get; set; }
        public bool Enabled { get; set; }
        public int? SeasonNumber { get; set; }
    }
}