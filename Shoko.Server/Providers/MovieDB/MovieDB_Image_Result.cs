﻿using TMDbLib.Objects.General;

namespace Shoko.Server.Providers.MovieDB;

public class MovieDB_Image_Result
{
    public string ImageID { get; set; }
    public string ImageType { get; set; }
    public string ImageSize { get; set; }
    public string URL { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }

    public override string ToString()
    {
        return string.Format("{0} - {1} - {2}x{3} - {4}", ImageType, ImageSize, ImageWidth, ImageHeight, URL);
    }

    public bool Populate(ImageData result, string imgType)
    {
        ImageID = string.Empty;
        ImageType = imgType;
        ImageSize = Shoko.Models.Constants.MovieDBImageSize.Original;
        URL = result.FilePath;
        ImageWidth = result.Width;
        ImageHeight = result.Height;

        return true;
    }
}
