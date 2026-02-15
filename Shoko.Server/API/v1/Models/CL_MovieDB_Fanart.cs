using System;

namespace Shoko.Server.API.v1.Models;

public class CL_MovieDB_Fanart : ICloneable
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

    public object Clone()
    {
        return new CL_MovieDB_Fanart
        {
            MovieDB_FanartID = MovieDB_FanartID,
            ImageID = ImageID,
            MovieId = MovieId,
            ImageType = ImageType,
            ImageSize = ImageSize,
            URL = URL,
            ImageWidth = ImageWidth,
            ImageHeight = ImageHeight,
            Enabled = Enabled
        };
    }
}
