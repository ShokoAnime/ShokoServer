using System;
using Shoko.Models.Interfaces;

namespace Shoko.Models.Server
{
    public class MovieDB_Poster : IImageEntity, ICloneable
    {
        public MovieDB_Poster()
        {
        }
        public int MovieDB_PosterID { get; set; }
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
            return new MovieDB_Poster
            {
                MovieDB_PosterID = MovieDB_PosterID,
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
}
