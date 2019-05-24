using Shoko.Database.Models.TvDB.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models.TvDB
{
    [Table("TvDB_ImageWideBanner")]
    public class ImageWideBanner : TvDBImage
    {
        [Key] public int TvDB_ImageWideBanner { get; set; }
    }
}