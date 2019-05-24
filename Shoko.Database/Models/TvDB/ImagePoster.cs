using Shoko.Database.Models.TvDB.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.TvDB
{
    [Table("TvDB_ImagePoster")]
    class ImagePoster : TvDBImage
    {
        [Key] public int TvDB_ImagePosterID { get; set; }
    }
}