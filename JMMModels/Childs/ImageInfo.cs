namespace JMMModels.Childs
{
    public class ImageInfo : IImageInfo
    {
        public string ImageLocalPath { get; set; }
        public ImageType ImageType { get; set; }
        public bool ImageEnabled { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public ImageSourceTypes ImageSource { get; set; }
    }

    public class ImageInfoWithDateUpdate : DateUpdate, IImageInfo
    {
        public string ImageLocalPath { get; set; }
        public ImageType ImageType { get; set; }
        public bool ImageEnabled { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public ImageSourceTypes ImageSource { get; set; }
    }

    public interface IImageInfo
    {
        string ImageLocalPath { get; set; }
        int ImageWidth { get; set; }
        int ImageHeight { get; set; }
        ImageSourceTypes ImageSource { get; set; }
        ImageType ImageType { get; set; }
        bool ImageEnabled { get; set; }
    }
}
