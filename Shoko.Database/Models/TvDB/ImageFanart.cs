using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.TvDB
{
    [Table("TvDB_ImageFanart")]
    class ImageFanart
    {
        [Key] public int TvDB_ImageFanartID { get; set; }
        public int Id { get; set; }
        public int SeriesID { get; set; } //again, this is a FK to a non primary key.... this should be referencing a PK.
        [MaxLength(200)] public string BannerPath { get; set; }
        [MaxLength(200)] public string BannerType { get; set; }
        [MaxLength(200)] public string BannerType2 { get; set; }
        [MaxLength(200)] public string Colors { get; set; }
        [MaxLength(200)] public string Language { get; set; }
        [MaxLength(200)] public string ThumbnailPath { get; set; }
        [MaxLength(200)] public string VignettePath { get; set; }
        [MaxLength(200)] public bool Enabled { get; set; }
        [MaxLength(200)] public bool Chosen { get; set; }
    }
}
