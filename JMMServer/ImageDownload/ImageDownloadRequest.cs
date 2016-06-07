namespace JMMServer.ImageDownload
{
    public class ImageDownloadRequest
    {
        public ImageDownloadRequest(JMMImageType imageType, object data, bool forceDownload)
        {
            ImageType = imageType;
            ImageData = data;
            ForceDownload = forceDownload;
        }

        public JMMImageType ImageType { get; set; }
        public object ImageData { get; set; }
        public bool ForceDownload { get; set; }
    }
}